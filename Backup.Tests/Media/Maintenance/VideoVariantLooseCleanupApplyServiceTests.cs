using System.Security.Cryptography;
using System.Text.Json;
using Backup.Infrastructure.Media.Data;
using Backup.Infrastructure.Media.Maintenance;

namespace Backup.Tests;

public sealed class VideoVariantLooseCleanupApplyServiceTests : IDisposable
{
    private const string Remove = "post/video/media/mp4/low/320x180.mp4";
    private const string Keep = "post/video/media/mp4/high/1280x720.mp4";
    private const string Remove2 = "post/video/media/mp4/low/640x360.mp4";
    private const string Keep2 = "post/video/media/mp4/high/1920x1080.mp4";
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"loose-apply-{Guid.NewGuid():N}"
    );

    [Fact]
    public async Task Prepare_RejectsUnsafeSourcesAndOperationHashIgnoresCreatedUtc()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace);
        VideoVariantLooseCleanupApplyService service = new(database);
        VideoVariantLooseCleanupManifest valid = Source(setup);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(valid with { PhysicalChecked = false })
        );
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(Sign(valid with { SelectionLimit = 1 }))
        );
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(Sign(valid with { Issues = [new(Remove, "fatal", "bad")] }))
        );
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.PrepareAsync(
                    Sign(valid with { Operations = [valid.Operations[0], valid.Operations[0]] })
                )
        );
        VideoVariantLooseCleanupManifest removeKeep = valid with
        {
            Operations =
            [
                Clone(valid.Operations[0], keepIndex: valid.Operations[0].RemovePathIndex),
            ],
        };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(Sign(removeKeep))
        );
        VideoVariantLooseCleanupManifest keepRemoved = valid with
        {
            Paths = [Remove, Keep, "other/video/media/mp4/x/640x360.mp4"],
            Operations =
            [
                valid.Operations[0],
                Clone(valid.Operations[0], removeIndex: 1, keepIndex: 2),
            ],
        };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(Sign(keepRemoved))
        );

        VideoVariantLooseCleanupApplyManifest prepared = await service.PrepareAsync(valid);
        VideoVariantLooseCleanupApplyManifest changedTime = prepared with
        {
            CreatedUtc = prepared.CreatedUtc.AddDays(1),
        };
        Assert.Equal(
            prepared.OperationSetHash,
            VideoVariantLooseCleanupApplyService.CalculateOperationSetHash(changedTime)
        );
        Assert.NotEqual(
            prepared.ApprovalHash,
            VideoVariantLooseCleanupApplyService.CalculateApprovalHash(changedTime)
        );
        Assert.Equal(setup.PrimaryNamespace, prepared.PrimaryNamespace);
        Assert.Equal(database.Namespaces.IncrementalNamespaces, prepared.IncrementalNamespaces);
        Assert.Equal(3, prepared.Version);
        Assert.Equal(1, prepared.Operations[0].KeepPartitionId);
        Assert.Throws<InvalidOperationException>(
            () =>
                service.SaveManifest(
                    prepared with
                    {
                        Version = 2,
                    },
                    Path.Combine(_root, "obsolete-v2.json")
                )
        );

        prepared.Operations[0] = Clone(prepared.Operations[0], keepPartitionId: 2);
        Assert.NotEqual(
            prepared.OperationSetHash,
            VideoVariantLooseCleanupApplyService.CalculateOperationSetHash(prepared)
        );
    }

    [Fact]
    public async Task Prepare_UsesDatabaseKeepPartitionAndApplyIgnoresDecoyKeepRoot()
    {
        Setup setup = CreateSetup(direct: false, keepPartitionId: 2, decoyKeep: true);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        database.PartitionIds[Keep] = 2;
        VideoVariantLooseCleanupApplyService service = new(database);

        VideoVariantLooseCleanupApplyManifest manifest = await service.PrepareAsync(Source(setup));
        Assert.Equal(2, manifest.Operations[0].KeepPartitionId);

        VideoVariantLooseCleanupReceipt receipt = await service.ApplyAsync(
            manifest,
            manifest.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            runtimeStoppedConfirmed: true
        );

        Assert.Equal(1, receipt.Operations);
        Assert.True(File.Exists(setup.KeepPath));
    }

    [Fact]
    public async Task Prepare_AllowsOnlyUnreferencedInvalidThumbnailGroups()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace);
        VideoVariantLooseCleanupApplyService service = new(database);
        VideoVariantLooseCleanupManifest valid = Source(setup);
        const string skipped = "other/video/media/thumb/source/jpg/size/small.jpg";

        VideoVariantLooseCleanupApplyManifest prepared = await service.PrepareAsync(
            Sign(valid with { Issues = [new(skipped, "keep-set-invalid", "incomplete")] })
        );
        Assert.Single(prepared.Operations);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.PrepareAsync(
                    Sign(valid with { Issues = [new(skipped, "cache-stale", "fatal")] })
                )
        );
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.PrepareAsync(
                    Sign(valid with { Issues = [new(Remove, "keep-set-invalid", "not a thumb")] })
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_QuarantinesFilesDeletesRowsWritesReceiptAndRetainsQuarantine(
        bool direct
    )
    {
        Setup setup = CreateSetup(direct);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyService service = new(database);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);

        VideoVariantLooseCleanupReceipt receipt = await service.ApplyAsync(
            manifest,
            manifest.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            runtimeStoppedConfirmed: true
        );

        Assert.False(File.Exists(setup.RemovePath));
        Assert.True(File.Exists(setup.KeepPath));
        Assert.False(File.Exists(setup.Journal));
        Assert.True(File.Exists(setup.Receipt));
        Assert.Empty(database.Rows);
        Assert.Equal(1, receipt.OriginalsQuarantined);
        Assert.Equal(direct ? 1 : 0, receipt.DirectQuarantined);
        Assert.All(receipt.QuarantineRoots, path => Assert.True(Directory.Exists(path)));
        Assert.True(
            Directory.EnumerateFiles(setup.MediaRoot, "*.bin", SearchOption.AllDirectories).Any()
        );
        if (direct)
            Assert.True(
                Directory
                    .EnumerateFiles(setup.DirectRoot, "*.bin", SearchOption.AllDirectories)
                    .Any()
            );
    }

    [Fact]
    public async Task Apply_ParallelPhysicalWorkPreservesTransactionalResult()
    {
        Setup setup = CreateSetup(direct: true);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);

        VideoVariantLooseCleanupReceipt receipt = await new VideoVariantLooseCleanupApplyService(
            database
        ).ApplyParallelAsync(
            manifest,
            manifest.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            batchSize: 1000,
            resume: false,
            runtimeStoppedConfirmed: true,
            physicalParallelism: 4
        );

        Assert.Equal(1, receipt.Operations);
        Assert.False(File.Exists(setup.RemovePath));
        Assert.True(File.Exists(setup.KeepPath));
        Assert.Empty(database.Rows);
        Assert.False(File.Exists(setup.Journal));
    }

    [Theory]
    [InlineData("after-direct-move")]
    [InlineData("after-original-move")]
    [InlineData("after-quarantined-journal")]
    [InlineData("after-db-commit")]
    public async Task Apply_CrashStagesResumeDeterministically(string stage)
    {
        Setup setup = CreateSetup(direct: true);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        VideoVariantLooseCleanupApplyService crashing = new(database)
        {
            FaultInjection = value =>
            {
                if (value == stage)
                    throw new TestCrashException();
            },
        };

        await Assert.ThrowsAsync<TestCrashException>(
            () =>
                crashing.ApplyAsync(
                    manifest,
                    manifest.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    runtimeStoppedConfirmed: true
                )
        );

        VideoVariantLooseCleanupReceipt receipt = await new VideoVariantLooseCleanupApplyService(
            database
        ).ApplyAsync(
            manifest,
            manifest.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            resume: true,
            runtimeStoppedConfirmed: true
        );
        Assert.Equal(1, receipt.Operations);
        Assert.Empty(database.Rows);
        Assert.False(File.Exists(setup.Journal));
    }

    [Fact]
    public async Task Apply_DestinationConflictAbortsBeforeDatabaseDelete()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        string hash = Sha256($"original\n1\n{Remove}");
        string destination = Path.Combine(
            setup.MediaRoot,
            ".video-variant-loose-quarantine",
            manifest.ApprovalHash,
            "original",
            "1",
            hash[..2],
            hash + ".bin"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantLooseCleanupApplyService(database).ApplyAsync(
                    manifest,
                    manifest.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    runtimeStoppedConfirmed: true
                )
        );

        Assert.True(File.Exists(setup.RemovePath));
        Assert.Equal(0, database.DeleteCalls);
    }

    [Fact]
    public async Task Apply_RejectsFingerprintApprovalRuntimeIndexesAndLockBeforeMutation()
    {
        Setup setup = CreateSetup(direct: false);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);

        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyService service = new(database);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.ApplyAsync(
                    manifest,
                    "wrong",
                    setup.Journal,
                    setup.Receipt,
                    runtimeStoppedConfirmed: true
                )
        );
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApplyAsync(manifest, manifest.ApprovalHash, setup.Journal, setup.Receipt)
        );

        database.Indexes = new(false, true);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.ApplyAsync(
                    manifest,
                    manifest.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    runtimeStoppedConfirmed: true
                )
        );
        database.Indexes = new(true, true);
        database.LockAvailable = false;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.ApplyAsync(
                    manifest,
                    manifest.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    runtimeStoppedConfirmed: true
                )
        );

        File.AppendAllText(setup.ConfigPath, " ");
        FakeDatabase untouched = new(setup.PrimaryNamespace, Remove);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantLooseCleanupApplyService(untouched).ApplyAsync(
                    manifest,
                    manifest.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    runtimeStoppedConfirmed: true
                )
        );
        Assert.Equal(0, untouched.DiscoverCalls);
        Assert.True(File.Exists(setup.RemovePath));
        Assert.Equal(0, database.DeleteCalls);
    }

    [Fact]
    public async Task Apply_CancellationBeforeMovesIsRecoverable()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                new VideoVariantLooseCleanupApplyService(database).ApplyAsync(
                    manifest,
                    manifest.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    runtimeStoppedConfirmed: true,
                    token: cancellation.Token
                )
        );

        Assert.True(File.Exists(setup.RemovePath));
        Assert.Equal(0, database.DeleteCalls);
    }

    [Fact]
    public async Task Verify_RejectsTamperedReceipt()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        VideoVariantLooseCleanupApplyService service = new(database);
        await service.ApplyAsync(
            manifest,
            manifest.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            runtimeStoppedConfirmed: true
        );
        File.WriteAllText(
            setup.Receipt,
            File.ReadAllText(setup.Receipt).Replace("\"Operations\": 1", "\"Operations\": 2")
        );

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.VerifyAsync(manifest, setup.Receipt)
        );
    }

    [Fact]
    public async Task DirectDelete_DeletesPhysicalThenDatabaseAndVerifies()
    {
        Setup setup = CreateSetup(direct: true);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        string sourceJournal = WriteSourceJournal(manifest);
        VideoVariantLooseCleanupApplyService service = new(database);
        VideoVariantLooseCleanupDirectDeletePlan plan = await service.PrepareDirectDeleteAsync(
            manifest,
            sourceJournal,
            1
        );

        VideoVariantLooseCleanupDirectDeleteReceipt receipt = await service.ApplyDirectDeleteAsync(
            manifest,
            sourceJournal,
            plan,
            plan.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            1,
            false,
            true,
            2
        );
        await service.VerifyDirectDeleteAsync(manifest, sourceJournal, plan, setup.Receipt);

        Assert.Equal(1, receipt.Operations);
        Assert.False(File.Exists(setup.RemovePath));
        Assert.False(File.Exists(setup.DirectPath));
        Assert.True(File.Exists(setup.KeepPath));
        Assert.Empty(database.Rows);
        Assert.Equal(1, database.DeleteCalls);
    }

    [Fact]
    public async Task DirectDelete_CrashAfterFilesResumesDatabaseDelete()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        string sourceJournal = WriteSourceJournal(manifest);
        VideoVariantLooseCleanupDirectDeletePlan plan =
            await new VideoVariantLooseCleanupApplyService(database).PrepareDirectDeleteAsync(
                manifest,
                sourceJournal,
                1
            );
        VideoVariantLooseCleanupApplyService crashing = new(database)
        {
            FaultInjection = stage =>
            {
                if (stage == "after-direct-delete-files")
                    throw new TestCrashException();
            },
        };
        await Assert.ThrowsAsync<TestCrashException>(
            () =>
                crashing.ApplyDirectDeleteAsync(
                    manifest,
                    sourceJournal,
                    plan,
                    plan.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    1,
                    false,
                    true,
                    1
                )
        );
        Assert.False(File.Exists(setup.RemovePath));
        Assert.Contains(Remove, database.Rows);

        await new VideoVariantLooseCleanupApplyService(database).ApplyDirectDeleteAsync(
            manifest,
            sourceJournal,
            plan,
            plan.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            1,
            true,
            true,
            1
        );
        Assert.Empty(database.Rows);
    }

    [Fact]
    public async Task DirectDelete_PrepareRejectsUnsafeFilesAndRowCounts()
    {
        Setup changed = CreateSetup(direct: false);
        VideoVariantLooseCleanupApplyManifest changedManifest = ApplyManifest(changed);
        string changedJournal = WriteSourceJournal(changedManifest);
        File.AppendAllText(changed.RemovePath, "changed");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantLooseCleanupApplyService(
                    new FakeDatabase(changed.PrimaryNamespace, Remove)
                ).PrepareDirectDeleteAsync(changedManifest, changedJournal, 1)
        );

        Dispose();
        Setup missingKeep = CreateSetup(direct: false);
        VideoVariantLooseCleanupApplyManifest missingKeepManifest = ApplyManifest(missingKeep);
        string missingKeepJournal = WriteSourceJournal(missingKeepManifest);
        File.Delete(missingKeep.KeepPath);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantLooseCleanupApplyService(
                    new FakeDatabase(missingKeep.PrimaryNamespace, Remove)
                ).PrepareDirectDeleteAsync(missingKeepManifest, missingKeepJournal, 1)
        );

        Dispose();
        Setup rows = CreateSetup(direct: false);
        VideoVariantLooseCleanupApplyManifest rowsManifest = ApplyManifest(rows);
        string rowsJournal = WriteSourceJournal(rowsManifest);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantLooseCleanupApplyService(
                    new FakeDatabase(rows.PrimaryNamespace)
                ).PrepareDirectDeleteAsync(rowsManifest, rowsJournal, 1)
        );
        FakeDatabase duplicate = new(rows.PrimaryNamespace, Remove) { DuplicateRows = 1 };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantLooseCleanupApplyService(duplicate).PrepareDirectDeleteAsync(
                    rowsManifest,
                    rowsJournal,
                    1
                )
        );
    }

    [Fact]
    public async Task DirectDelete_RejectsTamperedPlanAndReceipt()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        string sourceJournal = WriteSourceJournal(manifest);
        VideoVariantLooseCleanupApplyService service = new(database);
        VideoVariantLooseCleanupDirectDeletePlan plan = await service.PrepareDirectDeleteAsync(
            manifest,
            sourceJournal,
            1
        );
        Assert.Throws<InvalidOperationException>(
            () =>
                service.SaveDirectDeletePlan(
                    plan with
                    {
                        Bytes = plan.Bytes + 1,
                    },
                    Path.Combine(_root, "tampered-plan.json")
                )
        );
        await service.ApplyDirectDeleteAsync(
            manifest,
            sourceJournal,
            plan,
            plan.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            1,
            false,
            true,
            1
        );
        File.WriteAllText(
            setup.Receipt,
            File.ReadAllText(setup.Receipt).Replace("\"Operations\": 1", "\"Operations\": 2")
        );
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.VerifyDirectDeleteAsync(manifest, sourceJournal, plan, setup.Receipt)
        );
    }

    [Fact]
    public async Task DirectDelete_RejectsTamperedAndForgedJournalState()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        string sourceJournal = WriteSourceJournal(manifest);
        VideoVariantLooseCleanupDirectDeletePlan plan =
            await new VideoVariantLooseCleanupApplyService(database).PrepareDirectDeleteAsync(
                manifest,
                sourceJournal,
                1
            );
        VideoVariantLooseCleanupApplyService crashing = new(database)
        {
            FaultInjection = stage =>
            {
                if (stage == "after-direct-delete-files")
                    throw new TestCrashException();
            },
        };
        await Assert.ThrowsAsync<TestCrashException>(
            () =>
                crashing.ApplyDirectDeleteAsync(
                    manifest,
                    sourceJournal,
                    plan,
                    plan.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    1,
                    false,
                    true,
                    1
                )
        );

        string originalJournal = File.ReadAllText(setup.Journal);
        File.WriteAllText(
            setup.Journal,
            originalJournal.Replace("\"deleting-files\"", "\"db-committed\"")
        );
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantLooseCleanupApplyService(database).ApplyDirectDeleteAsync(
                    manifest,
                    sourceJournal,
                    plan,
                    plan.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    1,
                    true,
                    true,
                    1
                )
        );

        VideoVariantLooseCleanupDirectDeleteJournal forged =
            JsonSerializer.Deserialize<VideoVariantLooseCleanupDirectDeleteJournal>(
                originalJournal
            )!;
        forged.Batches[0].State = "db-committed";
        forged.Batches[0].FilesDeleted = plan.Files;
        forged.Batches[0].BytesDeleted = plan.Bytes;
        forged.JournalHash = VideoVariantLooseCleanupApplyService.CalculateDirectDeleteJournalHash(
            forged
        );
        File.WriteAllText(setup.Journal, JsonSerializer.Serialize(forged));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantLooseCleanupApplyService(database).ApplyDirectDeleteAsync(
                    manifest,
                    sourceJournal,
                    plan,
                    plan.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    1,
                    true,
                    true,
                    1
                )
        );
        Assert.Contains(Remove, database.Rows);
    }

    [Fact]
    public async Task DirectDelete_ExistingReceiptRejectsRecreatedFileOrRow()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        string sourceJournal = WriteSourceJournal(manifest);
        VideoVariantLooseCleanupApplyService service = new(database);
        VideoVariantLooseCleanupDirectDeletePlan plan = await service.PrepareDirectDeleteAsync(
            manifest,
            sourceJournal,
            1
        );
        await service.ApplyDirectDeleteAsync(
            manifest,
            sourceJournal,
            plan,
            plan.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            1,
            false,
            true,
            1
        );

        Write(setup.MediaRoot, Remove, "recreated");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.ApplyDirectDeleteAsync(
                    manifest,
                    sourceJournal,
                    plan,
                    plan.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    1,
                    false,
                    true,
                    1
                )
        );
        File.Delete(setup.RemovePath);
        database.Rows.Add(Remove);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.ApplyDirectDeleteAsync(
                    manifest,
                    sourceJournal,
                    plan,
                    plan.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    1,
                    false,
                    true,
                    1
                )
        );
    }

    [Fact]
    public async Task DirectDelete_RejectsSmallUnexpectedDirectBeforeDelete()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        string sourceJournal = WriteSourceJournal(manifest);
        Write(setup.DirectRoot, Remove, "unexpected");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantLooseCleanupApplyService(database).PrepareDirectDeleteAsync(
                    manifest,
                    sourceJournal,
                    1
                )
        );
        Assert.True(File.Exists(setup.RemovePath));
        Assert.Equal(0, database.DeleteCalls);
    }

    [Fact]
    public async Task DirectDelete_VerifyRejectsNamespaceDrift()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        string sourceJournal = WriteSourceJournal(manifest);
        VideoVariantLooseCleanupApplyService service = new(database);
        VideoVariantLooseCleanupDirectDeletePlan plan = await service.PrepareDirectDeleteAsync(
            manifest,
            sourceJournal,
            1
        );
        await service.ApplyDirectDeleteAsync(
            manifest,
            sourceJournal,
            plan,
            plan.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            1,
            false,
            true,
            1
        );
        database.Namespaces = new(
            setup.PrimaryNamespace,
            ["backup:media-cache:store:cache|incremental|changed"]
        );

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.VerifyDirectDeleteAsync(manifest, sourceJournal, plan, setup.Receipt)
        );
    }

    [Fact]
    public async Task Apply_SameRunResumeRejectsMissingQuarantine()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        VideoVariantLooseCleanupApplyService crashing = new(database)
        {
            FaultInjection = stage =>
            {
                if (stage == "after-db-commit")
                    throw new TestCrashException();
            },
        };
        await Assert.ThrowsAsync<TestCrashException>(
            () =>
                crashing.ApplyAsync(
                    manifest,
                    manifest.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    runtimeStoppedConfirmed: true
                )
        );
        string quarantined = Directory
            .EnumerateFiles(setup.MediaRoot, "*.bin", SearchOption.AllDirectories)
            .Single();
        File.Delete(quarantined);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantLooseCleanupApplyService(database).ApplyAsync(
                    manifest,
                    manifest.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    resume: true,
                    runtimeStoppedConfirmed: true
                )
        );
    }

    [Fact]
    public async Task Reconcile_SelectsOnlyNonPendingDeletesQuarantinedRowsAndSupportsPartialPurge()
    {
        Setup setup = CreateSetup(direct: true);
        VideoVariantLooseCleanupApplyManifest manifest = AddSecondOperation(setup);
        string eligiblePath = Resolve(setup.MediaRoot, Remove2);
        string eligibleQuarantine = QuarantinePath(setup.MediaRoot, manifest, 1, Remove2);
        Directory.CreateDirectory(Path.GetDirectoryName(eligibleQuarantine)!);
        File.Move(eligiblePath, eligibleQuarantine);
        File.Delete(setup.RemovePath);
        File.Delete(setup.DirectPath!);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove2);
        string sourceJournal = WriteSourceJournal(manifest, "pending", "quarantined");
        byte[] frozen = File.ReadAllBytes(sourceJournal);
        VideoVariantLooseCleanupApplyService service = new(database);

        VideoVariantLooseCleanupReceipt receipt = await service.ReconcileLegacyJournalAsync(
            manifest,
            sourceJournal,
            manifest.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            false,
            true,
            2
        );

        Assert.Equal([1], receipt.OperationIndices);
        Assert.Equal(1, receipt.Operations);
        Assert.Equal(1, receipt.PrimaryDeleted);
        Assert.Equal(frozen, File.ReadAllBytes(sourceJournal));
        Assert.DoesNotContain(Remove, database.RowCountPaths);
        Assert.Contains(Remove2, database.RowCountPaths);
        database.Rows.Add(Remove);
        await service.VerifyAsync(manifest, setup.Receipt);

        string pendingQuarantine = QuarantinePath(setup.MediaRoot, manifest, 1, Remove);
        Directory.CreateDirectory(Path.GetDirectoryName(pendingQuarantine)!);
        File.WriteAllText(pendingQuarantine, "original");
        string drive = Path.GetPathRoot(setup.MediaRoot)![0].ToString();
        VideoVariantLooseCleanupPurgeService purge = new();
        VideoVariantLooseCleanupPurgePlan plan = purge.Plan(manifest, setup.Receipt, drive);
        Assert.Equal(1, plan.Files);
        await purge.PurgeAsync(
            manifest,
            setup.Receipt,
            drive,
            receipt.ReceiptHash,
            Path.Combine(_root, "partial-purge.journal.json"),
            Path.Combine(_root, "partial-purge.receipt.json"),
            runtimeStoppedConfirmed: true
        );
        Assert.False(File.Exists(eligibleQuarantine));
        Assert.True(File.Exists(pendingQuarantine));
    }

    [Fact]
    public void PhysicalPathComparer_IsOrdinalOnLinuxAndIgnoreCaseOnWindows()
    {
        Assert.False(PhysicalPathSemantics.ComparerFor(windows: false).Equals("/mnt/A", "/mnt/a"));
        Assert.True(PhysicalPathSemantics.ComparerFor(windows: true).Equals("C:\\A", "c:\\a"));
        Assert.Equal(StringComparison.Ordinal, PhysicalPathSemantics.ComparisonFor(false));
        Assert.Equal(StringComparison.OrdinalIgnoreCase, PhysicalPathSemantics.ComparisonFor(true));
    }

    [Fact]
    public void PurgeSelector_ResolvesLinuxRootExactlyAndRejectsAmbiguousMount()
    {
        string[] roots =
        [
            "/mnt/l/.video-variant-loose-quarantine/approval",
            "/mnt/j/.video-variant-loose-quarantine/approval",
        ];

        Assert.Equal(
            roots[0],
            VideoVariantLooseCleanupPurgeService.SelectQuarantineRoot("/mnt/l", roots, false)
        );
        Assert.Equal(
            "/mnt/l",
            VideoVariantLooseCleanupPurgeService.NormalizeSelector("/mnt/l/", false)
        );
        Assert.Throws<InvalidOperationException>(
            () => VideoVariantLooseCleanupPurgeService.SelectQuarantineRoot("/mnt", roots, false)
        );
        Assert.Throws<InvalidOperationException>(
            () => VideoVariantLooseCleanupPurgeService.SelectQuarantineRoot("/mnt/L", roots, false)
        );
        Assert.Equal("L", VideoVariantLooseCleanupPurgeService.NormalizeSelector("l", true));
    }

    [Fact]
    public async Task Reconcile_MovingInfersMoveAndVerifiedRequiresRowsAbsent()
    {
        Setup moving = CreateSetup(direct: false);
        VideoVariantLooseCleanupApplyManifest movingManifest = ApplyManifest(moving);
        string movingSource = WriteSourceJournal(movingManifest, "moving");
        FakeDatabase movingDatabase = new(moving.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupReceipt receipt = await new VideoVariantLooseCleanupApplyService(
            movingDatabase
        ).ReconcileLegacyJournalAsync(
            movingManifest,
            movingSource,
            movingManifest.ApprovalHash,
            moving.Journal,
            moving.Receipt,
            false,
            true,
            1
        );
        Assert.Equal([0], receipt.OperationIndices);
        Assert.False(File.Exists(moving.RemovePath));
        Assert.Empty(movingDatabase.Rows);

        Dispose();
        Setup verified = CreateSetup(direct: false);
        VideoVariantLooseCleanupApplyManifest verifiedManifest = ApplyManifest(verified);
        string quarantine = QuarantinePath(verified.MediaRoot, verifiedManifest, 1, Remove);
        Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!);
        File.Move(verified.RemovePath, quarantine);
        string verifiedSource = WriteSourceJournal(verifiedManifest, "verified");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantLooseCleanupApplyService(
                    new FakeDatabase(verified.PrimaryNamespace, Remove)
                ).ReconcileLegacyJournalAsync(
                    verifiedManifest,
                    verifiedSource,
                    verifiedManifest.ApprovalHash,
                    verified.Journal,
                    verified.Receipt,
                    false,
                    true,
                    1
                )
        );
    }

    [Fact]
    public async Task Reconcile_RejectsTamperedReceiptAndLegacyReceiptOmitsPartialFields()
    {
        Setup setup = CreateSetup(direct: false);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        string quarantine = QuarantinePath(setup.MediaRoot, manifest, 1, Remove);
        Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!);
        File.Move(setup.RemovePath, quarantine);
        string source = WriteSourceJournal(manifest, "verified");
        VideoVariantLooseCleanupApplyService service = new(
            new FakeDatabase(setup.PrimaryNamespace)
        );
        await service.ReconcileLegacyJournalAsync(
            manifest,
            source,
            manifest.ApprovalHash,
            setup.Journal,
            setup.Receipt,
            false,
            true,
            1
        );
        File.WriteAllText(
            setup.Receipt,
            File.ReadAllText(setup.Receipt)
                .Replace("\"OperationIndices\": [", "\"OperationIndices\": [ 99,")
        );
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.ReconcileLegacyJournalAsync(
                    manifest,
                    source,
                    manifest.ApprovalHash,
                    setup.Journal,
                    setup.Receipt,
                    false,
                    true,
                    1
                )
        );

        string legacyJson = JsonSerializer.Serialize(new VideoVariantLooseCleanupReceipt());
        Assert.DoesNotContain("SourceJournalSha256", legacyJson);
        Assert.DoesNotContain("OperationIndices", legacyJson);
    }

    [Fact]
    public async Task Purge_PlansAndDeletesOnlySignedQuarantineFiles()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        VideoVariantLooseCleanupReceipt applyReceipt =
            await new VideoVariantLooseCleanupApplyService(database).ApplyAsync(
                manifest,
                manifest.ApprovalHash,
                setup.Journal,
                setup.Receipt,
                runtimeStoppedConfirmed: true
            );
        string drive = Path.GetPathRoot(setup.MediaRoot)![0].ToString();
        string purgeJournal = Path.Combine(_root, "purge.run.json");
        string purgeReceiptPath = Path.Combine(_root, "purge.receipt.json");
        VideoVariantLooseCleanupPurgeService service = new();

        VideoVariantLooseCleanupPurgePlan plan = service.Plan(manifest, setup.Receipt, drive);
        Assert.Equal(1, plan.Files);
        Assert.Equal(applyReceipt.BytesQuarantined, plan.Bytes);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.PurgeAsync(
                    manifest,
                    setup.Receipt,
                    drive,
                    applyReceipt.ReceiptHash,
                    purgeJournal,
                    purgeReceiptPath
                )
        );

        VideoVariantLooseCleanupPurgeReceipt purgeReceipt = await service.PurgeAsync(
            manifest,
            setup.Receipt,
            drive,
            applyReceipt.ReceiptHash,
            purgeJournal,
            purgeReceiptPath,
            batchSize: 1,
            runtimeStoppedConfirmed: true
        );

        Assert.Equal(1, purgeReceipt.FilesDeleted);
        Assert.Equal(applyReceipt.BytesQuarantined, purgeReceipt.BytesDeleted);
        Assert.False(File.Exists(purgeJournal));
        Assert.True(File.Exists(purgeReceiptPath));
        Assert.False(
            Directory.EnumerateFiles(setup.MediaRoot, "*.bin", SearchOption.AllDirectories).Any()
        );
        Assert.True(File.Exists(setup.KeepPath));
    }

    [Fact]
    public async Task Purge_CrashAfterDeleteResumesCurrentBatch()
    {
        Setup setup = CreateSetup(direct: false);
        FakeDatabase database = new(setup.PrimaryNamespace, Remove);
        VideoVariantLooseCleanupApplyManifest manifest = ApplyManifest(setup);
        VideoVariantLooseCleanupReceipt applyReceipt =
            await new VideoVariantLooseCleanupApplyService(database).ApplyAsync(
                manifest,
                manifest.ApprovalHash,
                setup.Journal,
                setup.Receipt,
                runtimeStoppedConfirmed: true
            );
        string drive = Path.GetPathRoot(setup.MediaRoot)![0].ToString();
        string purgeJournal = Path.Combine(_root, "purge-resume.run.json");
        string purgeReceiptPath = Path.Combine(_root, "purge-resume.receipt.json");
        VideoVariantLooseCleanupPurgeService crashing = new()
        {
            FaultInjection = value =>
            {
                if (value == "after-delete")
                    throw new TestCrashException();
            },
        };

        await Assert.ThrowsAsync<TestCrashException>(
            () =>
                crashing.PurgeAsync(
                    manifest,
                    setup.Receipt,
                    drive,
                    applyReceipt.ReceiptHash,
                    purgeJournal,
                    purgeReceiptPath,
                    batchSize: 1,
                    runtimeStoppedConfirmed: true
                )
        );
        Assert.True(File.Exists(purgeJournal));

        VideoVariantLooseCleanupPurgeReceipt purgeReceipt =
            await new VideoVariantLooseCleanupPurgeService().PurgeAsync(
                manifest,
                setup.Receipt,
                drive,
                applyReceipt.ReceiptHash,
                purgeJournal,
                purgeReceiptPath,
                batchSize: 1,
                resume: true,
                runtimeStoppedConfirmed: true
            );

        Assert.Equal(1, purgeReceipt.FilesDeleted);
        Assert.False(File.Exists(purgeJournal));
    }

    [Fact]
    public void Sql_UsesConcurrentExactIndexesAndSharedAdvisoryKey()
    {
        Assert.Equal(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_media_cache_primary_namespace_normalized_path ON media_cache_primary_entries (namespace, (lower(replace(path, chr(92), '/'))))",
            VideoVariantLooseCleanupApplyService.CreatePrimaryIndexSql
        );
        Assert.Equal(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_media_cache_incremental_namespace_normalized_path ON media_cache_incremental_entries (namespace, (lower(replace(path, chr(92), '/'))))",
            VideoVariantLooseCleanupApplyService.CreateIncrementalIndexSql
        );
        Assert.Contains(
            "DROP INDEX CONCURRENTLY IF EXISTS",
            VideoVariantLooseCleanupApplyService.DropInvalidPrimaryIndexSql
        );
        Assert.Contains(
            "DROP INDEX CONCURRENTLY IF EXISTS",
            VideoVariantLooseCleanupApplyService.DropInvalidIncrementalIndexSql
        );
        Assert.Contains(
            "pg_advisory_xact_lock",
            PostgresMediaCachePersistenceIOService.MutationLockSql
        );
        Assert.Contains(
            "'media-cache:' || @storeNamespace",
            PostgresMediaCachePersistenceIOService.MutationLockSql
        );
    }

    private Setup CreateSetup(bool direct, int keepPartitionId = 1, bool decoyKeep = false)
    {
        Directory.CreateDirectory(_root);
        string mediaRoot = Path.Combine(_root, "media");
        string directRoot = Path.Combine(_root, "direct");
        string keepRoot = keepPartitionId == 1 ? mediaRoot : Path.Combine(_root, "media-2");
        Directory.CreateDirectory(mediaRoot);
        Directory.CreateDirectory(directRoot);
        Directory.CreateDirectory(keepRoot);
        string remove = Write(mediaRoot, Remove, "original");
        string keep = Write(keepRoot, Keep, "keep");
        if (decoyKeep)
            Write(mediaRoot, Keep, "decoy");
        string? directPath = direct ? Write(directRoot, Remove, "direct") : null;
        string config = Path.Combine(_root, "Data.json");
        File.WriteAllText(
            config,
            JsonSerializer.Serialize(
                new
                {
                    Aliases = new Dictionary<string, string>(),
                    Partitions = keepPartitionId == 1
                        ? new[] { new { Id = 1, Paths = new[] { mediaRoot } } }
                        :
                        [
                            new { Id = 1, Paths = new[] { mediaRoot } },
                            new { Id = 2, Paths = new[] { keepRoot } },
                        ],
                    Media = new[]
                    {
                        new
                        {
                            Id = "store",
                            Cache = new[]
                            {
                                new
                                {
                                    Id = "cache",
                                    Type = "postgres",
                                    ConnectionString = "Host=unused",
                                },
                            },
                            Paths = new { Media = new { Paths = Array.Empty<string>() } },
                            Partitions = keepPartitionId == 1 ? new[] { 1 } : [1, 2],
                        },
                    },
                }
            )
        );
        return new(
            config,
            mediaRoot,
            directRoot,
            remove,
            keep,
            directPath,
            keepPartitionId,
            Path.Combine(_root, "journal.json"),
            Path.Combine(_root, "receipt.json"),
            "backup:media-cache:store:cache|primary|main"
        );
    }

    private static VideoVariantLooseCleanupManifest Source(Setup setup)
    {
        FileInfo original = new(setup.RemovePath);
        FileInfo? direct = setup.DirectPath is null ? null : new(setup.DirectPath);
        FileInfo config = new(setup.ConfigPath);
        VideoVariantLooseCleanupManifest manifest = new()
        {
            Version = 1,
            CreatedUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            DataConfigPath = setup.ConfigPath,
            DataConfigLength = config.Length,
            DataConfigLastWriteUtcTicks = config.LastWriteTimeUtc.Ticks,
            MediaStoreId = "store",
            CacheId = "cache",
            CacheNamespace = setup.PrimaryNamespace,
            DirectRoot = setup.DirectRoot,
            FileParallelism = 1,
            SelectionLimit = null,
            PhysicalChecked = true,
            Paths = [Remove, Keep],
            Operations =
            [
                new()
                {
                    RemovePathIndex = 0,
                    KeepPathIndex = 1,
                    PartitionId = 1,
                    OriginalSize = original.Length,
                    OriginalLastWriteUtcTicks = original.LastWriteTimeUtc.Ticks,
                    DirectPresent = direct is not null,
                    DirectSize = direct?.Length ?? 0,
                    DirectLastWriteUtcTicks = direct?.LastWriteTimeUtc.Ticks ?? 0,
                },
            ],
        };
        return Sign(manifest);
    }

    private static VideoVariantLooseCleanupApplyManifest ApplyManifest(Setup setup)
    {
        VideoVariantLooseCleanupManifest source = Source(setup);
        byte[] config = File.ReadAllBytes(setup.ConfigPath);
        VideoVariantLooseCleanupApplyManifest manifest = new()
        {
            Version = 3,
            CreatedUtc = source.CreatedUtc,
            SourceManifestApprovalHash = source.ApprovalHash,
            DataConfigPath = source.DataConfigPath,
            DataConfigLength = source.DataConfigLength,
            DataConfigLastWriteUtcTicks = source.DataConfigLastWriteUtcTicks,
            DataConfigSha256 = Convert.ToHexString(SHA256.HashData(config)).ToLowerInvariant(),
            MediaStoreId = source.MediaStoreId,
            CacheId = source.CacheId,
            PrimaryNamespace = source.CacheNamespace,
            IncrementalNamespaces = ["backup:media-cache:store:cache|incremental|one"],
            DirectRoot = source.DirectRoot,
            Paths = [.. source.Paths],
            Operations = source
                .Operations.Select(value => new VideoVariantLooseCleanupApplyOperation
                {
                    RemovePathIndex = value.RemovePathIndex,
                    KeepPathIndex = value.KeepPathIndex,
                    PartitionId = value.PartitionId,
                    KeepPartitionId = setup.KeepPartitionId,
                    OriginalSize = value.OriginalSize,
                    OriginalLastWriteUtcTicks = value.OriginalLastWriteUtcTicks,
                    DirectPresent = value.DirectPresent,
                    DirectSize = value.DirectSize,
                    DirectLastWriteUtcTicks = value.DirectLastWriteUtcTicks,
                })
                .ToList(),
        };
        manifest.OperationSetHash = VideoVariantLooseCleanupApplyService.CalculateOperationSetHash(
            manifest
        );
        manifest.ApprovalHash = VideoVariantLooseCleanupApplyService.CalculateApprovalHash(
            manifest
        );
        return manifest;
    }

    private static VideoVariantLooseCleanupManifest Sign(VideoVariantLooseCleanupManifest manifest)
    {
        manifest.ApprovalHash = VideoVariantLooseCleanupPreviewService.CalculateApprovalHash(
            manifest
        );
        return manifest;
    }

    private string WriteSourceJournal(VideoVariantLooseCleanupApplyManifest manifest)
    {
        string path = Path.Combine(_root, "source-journal.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new
                {
                    manifest.ApprovalHash,
                    manifest.OperationSetHash,
                    RunId = Guid.NewGuid(),
                    StartedUtc = DateTimeOffset.UtcNow,
                    BatchSize = 1,
                    Batches = new[] { new { Index = 0, State = "pending" } },
                }
            )
        );
        return path;
    }

    private string WriteSourceJournal(
        VideoVariantLooseCleanupApplyManifest manifest,
        params string[] states
    )
    {
        string path = Path.Combine(_root, $"source-journal-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new
                {
                    manifest.ApprovalHash,
                    manifest.OperationSetHash,
                    RunId = Guid.NewGuid(),
                    StartedUtc = DateTimeOffset.UtcNow,
                    BatchSize = 1,
                    Batches = states.Select(
                        (state, index) =>
                        {
                            VideoVariantLooseCleanupApplyOperation operation = manifest.Operations[
                                index
                            ];
                            bool quarantined =
                                state is "quarantined" or "db-committed" or "verified";
                            return new
                            {
                                Index = index,
                                State = state,
                                OriginalsQuarantined = quarantined ? 1 : 0,
                                DirectQuarantined = quarantined && operation.DirectPresent ? 1 : 0,
                                BytesQuarantined = quarantined
                                    ? operation.OriginalSize
                                        + (operation.DirectPresent ? operation.DirectSize : 0)
                                    : 0,
                                PrimaryDeleted = 0,
                                IncrementalDeleted = 0,
                            };
                        }
                    ),
                }
            )
        );
        return path;
    }

    private static VideoVariantLooseCleanupApplyManifest AddSecondOperation(Setup setup)
    {
        string remove = Write(setup.MediaRoot, Remove2, "eligible");
        string keep = Write(setup.MediaRoot, Keep2, "keep-two");
        FileInfo removeInfo = new(remove);
        VideoVariantLooseCleanupApplyManifest first = ApplyManifest(setup);
        first.Paths.Add(Remove2);
        first.Paths.Add(Keep2);
        first.Operations.Add(
            new()
            {
                RemovePathIndex = 2,
                KeepPathIndex = 3,
                PartitionId = 1,
                KeepPartitionId = 1,
                OriginalSize = removeInfo.Length,
                OriginalLastWriteUtcTicks = removeInfo.LastWriteTimeUtc.Ticks,
            }
        );
        first.OperationSetHash = VideoVariantLooseCleanupApplyService.CalculateOperationSetHash(
            first
        );
        first.ApprovalHash = VideoVariantLooseCleanupApplyService.CalculateApprovalHash(first);
        Assert.True(File.Exists(keep));
        return first;
    }

    private static string QuarantinePath(
        string root,
        VideoVariantLooseCleanupApplyManifest manifest,
        int partitionId,
        string logical
    )
    {
        string hash = Sha256($"original\n{partitionId}\n{logical}");
        return Path.Combine(
            root,
            ".video-variant-loose-quarantine",
            manifest.ApprovalHash,
            "original",
            partitionId.ToString(),
            hash[..2],
            hash + ".bin"
        );
    }

    private static string Resolve(string root, string logical) =>
        Path.Combine(root, logical.Replace('/', Path.DirectorySeparatorChar));

    private static VideoVariantLooseCleanupOperation Clone(
        VideoVariantLooseCleanupOperation value,
        int? removeIndex = null,
        int? keepIndex = null
    ) =>
        new()
        {
            RemovePathIndex = removeIndex ?? value.RemovePathIndex,
            KeepPathIndex = keepIndex ?? value.KeepPathIndex,
            PartitionId = value.PartitionId,
            OriginalSize = value.OriginalSize,
            OriginalLastWriteUtcTicks = value.OriginalLastWriteUtcTicks,
            DirectPresent = value.DirectPresent,
            DirectSize = value.DirectSize,
            DirectLastWriteUtcTicks = value.DirectLastWriteUtcTicks,
        };

    private static VideoVariantLooseCleanupApplyOperation Clone(
        VideoVariantLooseCleanupApplyOperation value,
        int keepPartitionId
    ) =>
        new()
        {
            RemovePathIndex = value.RemovePathIndex,
            KeepPathIndex = value.KeepPathIndex,
            PartitionId = value.PartitionId,
            KeepPartitionId = keepPartitionId,
            OriginalSize = value.OriginalSize,
            OriginalLastWriteUtcTicks = value.OriginalLastWriteUtcTicks,
            DirectPresent = value.DirectPresent,
            DirectSize = value.DirectSize,
            DirectLastWriteUtcTicks = value.DirectLastWriteUtcTicks,
        };

    private static string Write(string root, string logical, string content)
    {
        string path = Path.Combine(root, logical.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Sha256(string value) =>
        Convert
            .ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed record Setup(
        string ConfigPath,
        string MediaRoot,
        string DirectRoot,
        string RemovePath,
        string KeepPath,
        string? DirectPath,
        int KeepPartitionId,
        string Journal,
        string Receipt,
        string PrimaryNamespace
    );

    private sealed class TestCrashException : Exception;

    private sealed class FakeDatabase(string primaryNamespace, params string[] rows)
        : IVideoVariantLooseCleanupDatabase
    {
        public VideoVariantLooseCleanupNamespaces Namespaces { get; set; } =
            new(primaryNamespace, ["backup:media-cache:store:cache|incremental|one"]);
        public HashSet<string> Rows { get; } = rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
        public VideoVariantLooseCleanupIndexStatus Indexes { get; set; } = new(true, true);
        public bool LockAvailable { get; set; } = true;
        public int DiscoverCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public int DuplicateRows { get; set; }
        public Dictionary<string, int> PartitionIds { get; } =
            new(StringComparer.Ordinal) { [Keep] = 1 };
        public List<string> RowCountPaths { get; } = [];

        public Task<VideoVariantLooseCleanupNamespaces> DiscoverNamespacesAsync(
            string storeNamespace,
            CancellationToken token
        )
        {
            DiscoverCalls++;
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Namespaces);
        }

        public Task<VideoVariantLooseCleanupIndexStatus> CheckIndexesAsync(
            CancellationToken token
        ) => Task.FromResult(Indexes);

        public Task<IReadOnlyDictionary<string, int>> LoadPartitionIdsAsync(
            string primaryNamespace,
            IReadOnlyCollection<string> normalizedPaths,
            CancellationToken token
        )
        {
            token.ThrowIfCancellationRequested();
            Dictionary<string, int> result = new(StringComparer.Ordinal);

            foreach (string path in normalizedPaths)
            {
                if (!PartitionIds.TryGetValue(path, out int partitionId))
                    throw new InvalidOperationException(
                        $"Keep path is missing from primary cache: {path}"
                    );
                result.Add(path, partitionId);
            }
            return Task.FromResult<IReadOnlyDictionary<string, int>>(result);
        }

        public Task CreateIndexesAsync(CancellationToken token) => Task.CompletedTask;

        public Task<IAsyncDisposable?> TryAcquireLockAsync(
            string storeNamespace,
            CancellationToken token
        ) => Task.FromResult<IAsyncDisposable?>(LockAvailable ? new Lease() : null);

        public Task<VideoVariantLooseCleanupDeleteResult> DeleteBatchAsync(
            string primaryNamespace,
            IReadOnlyList<string> incrementalNamespaces,
            IReadOnlyList<string> normalizedPaths,
            CancellationToken token
        )
        {
            token.ThrowIfCancellationRequested();
            DeleteCalls++;
            int deleted = normalizedPaths.Count(path => Rows.Remove(path));
            return Task.FromResult(new VideoVariantLooseCleanupDeleteResult(deleted, 0));
        }

        public Task<bool> AreRowsAbsentAsync(
            string primaryNamespace,
            IReadOnlyList<string> incrementalNamespaces,
            IReadOnlyList<string> normalizedPaths,
            CancellationToken token
        ) => Task.FromResult(normalizedPaths.All(path => !Rows.Contains(path)));

        public Task<IReadOnlyDictionary<string, int>> LoadRowCountsAsync(
            string primaryNamespace,
            IReadOnlyList<string> incrementalNamespaces,
            IReadOnlyList<string> normalizedPaths,
            CancellationToken token
        )
        {
            RowCountPaths.AddRange(normalizedPaths);
            return Task.FromResult<IReadOnlyDictionary<string, int>>(
                normalizedPaths.ToDictionary(
                    path => path,
                    path => Rows.Contains(path) ? 1 + DuplicateRows : 0,
                    StringComparer.Ordinal
                )
            );
        }

        private sealed class Lease : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
