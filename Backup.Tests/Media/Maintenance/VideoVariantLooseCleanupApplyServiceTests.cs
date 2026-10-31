using System.Security.Cryptography;
using System.Text.Json;
using Backup.Infrastructure.Media.Data;
using Backup.Infrastructure.Media.Maintenance;

namespace Backup.Tests;

public sealed class VideoVariantLooseCleanupApplyServiceTests : IDisposable
{
    private const string Remove = "post/video/media/mp4/low/320x180.mp4";
    private const string Keep = "post/video/media/mp4/high/1280x720.mp4";
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
        public VideoVariantLooseCleanupNamespaces Namespaces { get; } =
            new(primaryNamespace, ["backup:media-cache:store:cache|incremental|one"]);
        public HashSet<string> Rows { get; } = rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
        public VideoVariantLooseCleanupIndexStatus Indexes { get; set; } = new(true, true);
        public bool LockAvailable { get; set; } = true;
        public int DiscoverCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public Dictionary<string, int> PartitionIds { get; } =
            new(StringComparer.Ordinal) { [Keep] = 1 };

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
            return Task.FromResult(new VideoVariantLooseCleanupDeleteResult(deleted, deleted));
        }

        public Task<bool> AreRowsAbsentAsync(
            string primaryNamespace,
            IReadOnlyList<string> incrementalNamespaces,
            IReadOnlyList<string> normalizedPaths,
            CancellationToken token
        ) => Task.FromResult(normalizedPaths.All(path => !Rows.Contains(path)));

        private sealed class Lease : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
