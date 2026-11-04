using Backup.Infrastructure.DependencyInjection.Composition;
using Backup.Infrastructure.DependencyInjection.Runtime;
using Backup.Infrastructure.Hosting;
using Backup.Infrastructure.Media.Maintenance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

if (args.Length >= 3 && args[0] == "maintenance" && args[1] == "video-variants")
{
    await RunVideoVariantMaintenance(args[2], args[3..]);
    return;
}

bool automaticMediaPrune = args.Length >= 3 && args[0] == "maintenance" && args[1] == "media-prune";

ServiceCollection services = new();
ConfigurationManager configuration = new();
configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
    optional: true,
    reloadOnChange: false
);
configuration.AddEnvironmentVariables();

services.AddRuntimeConfiguration(configuration);
services.AddBackupCliInfrastructure();

Console.Error.WriteLine("[startup] building service provider");
await using ServiceProvider provider = services.BuildServiceProvider();

Console.Error.WriteLine("[startup] creating scope");
await using AsyncServiceScope scope = provider.CreateAsyncScope();

Console.Error.WriteLine("[startup] running setup");
await scope.ServiceProvider.RunBackupInfrastructureSetup();

Console.Error.WriteLine("[startup] resolving app");
if (automaticMediaPrune)
{
    await RunAutomaticMediaPrune(scope.ServiceProvider, args[2], args[3..]);
    return;
}
BackupCliRunner cliRunner = scope.ServiceProvider.GetRequiredService<BackupCliRunner>();

using CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
    Console.Error.WriteLine("[shutdown] cancellation requested (Ctrl+C)");
};

Console.Error.WriteLine("[startup] running backup");
await cliRunner.RunBackup(cts.Token);

static async Task RunAutomaticMediaPrune(
    IServiceProvider provider,
    string operation,
    string[] options
)
{
    AutomaticLooseMediaPruneService service = provider.GetRequiredService<AutomaticLooseMediaPruneService>();
    string dataConfig = GetRequiredOption(options, "--data-config");
    string mediaStore = GetRequiredOption(options, "--media-store");
    string cache = GetRequiredOption(options, "--cache");
    int batchSize = int.Parse(GetOption(options, "--batch-size") ?? "1000");
    using CancellationTokenSource cts = new();
    void handler(object? _, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        cts.Cancel();
    }
    Console.CancelKeyPress += handler;
    try
    {
        Progress<AutomaticLooseMediaPruneProgress> progress = new(value =>
        {
            Console.Error.WriteLine(
                $"[{value.Phase}] operations: {value.Completed}/{value.Total}, bytes: {value.Bytes}"
            );
            Console.Error.Flush();
        });
        if (operation is "preview" or "prune")
        {
            AutomaticLooseMediaPrunePreviewResult result = operation == "prune"
                ? await service.PruneAsync(
                    dataConfig,
                    mediaStore,
                    cache,
                    progress,
                    cts.Token
                )
                : await service.PreviewAsync(
                    dataConfig,
                    mediaStore,
                    cache,
                    progress,
                    cts.Token
                );
            Console.WriteLine($"run: {result.RunId}");
            Console.WriteLine($"state: {result.State}");
            Console.WriteLine($"approval: {result.ApprovalHash}");
            Console.WriteLine($"policy hash: {result.PolicyHash}");
            Console.WriteLine($"projection hash: {result.ProjectionHash}");
            Console.WriteLine($"operations: {result.Operations}");
            Console.WriteLine($"planned bytes: {result.PlannedBytes}");
            return;
        }
        if (operation == "apply")
        {
            AutomaticLooseMediaPruneResult result = await service.ApplyAsync(
                dataConfig,
                mediaStore,
                cache,
                Guid.Parse(GetRequiredOption(options, "--run-id")),
                GetRequiredOption(options, "--approval"),
                HasFlag(options, "--runtime-stopped-confirmed"),
                batchSize,
                progress,
                cts.Token
            );
            Console.WriteLine($"run: {result.RunId}");
            Console.WriteLine($"state: {result.State}");
            Console.WriteLine($"operations: {result.Operations}");
            Console.WriteLine($"completed: {result.Completed}");
            Console.WriteLine($"bytes quarantined: {result.BytesQuarantined}");
            Console.WriteLine($"purge after: {result.PurgeAfterUtc:O}");
            return;
        }
        if (operation == "status")
        {
            IReadOnlyList<AutomaticLooseMediaPruneStatus> statuses = await service.StatusAsync(
                dataConfig,
                mediaStore,
                cache,
                cts.Token
            );
            foreach (AutomaticLooseMediaPruneStatus status in statuses)
            {
                Console.WriteLine(
                    $"{status.RunId}  {status.State}  {status.Completed}/{status.Operations}  {status.Bytes}/{status.PlannedBytes} bytes  approval={status.ApprovalHash}  purge-after={status.PurgeAfterUtc:O}"
                );
            }
            return;
        }
        if (operation == "cancel")
        {
            Guid runId = Guid.Parse(GetRequiredOption(options, "--run-id"));
            await service.CancelAsync(
                dataConfig,
                mediaStore,
                cache,
                runId,
                cts.Token
            );
            Console.WriteLine($"run: {runId}");
            Console.WriteLine("state: cancelled");
            return;
        }
        if (operation == "purge-expired")
        {
            AutomaticLooseMediaPurgeResult result = await service.PurgeExpiredAsync(
                dataConfig,
                mediaStore,
                cache,
                HasFlag(options, "--runtime-stopped-confirmed"),
                batchSize,
                progress,
                cts.Token
            );
            Console.WriteLine($"files deleted: {result.FilesDeleted}");
            Console.WriteLine($"bytes deleted: {result.BytesDeleted}");
            return;
        }
        throw new ArgumentException($"Unknown media prune operation: {operation}");
    }
    finally
    {
        Console.CancelKeyPress -= handler;
    }
}

static async Task RunVideoVariantMaintenance(string operation, string[] options)
{
    if (
        operation
        is "preview-loose-v1"
            or "preflight-loose-v1"
            or "prepare-apply-loose-v2"
            or "check-loose-indexes-v2"
            or "create-loose-indexes-v2"
            or "apply-loose-v2"
            or "verify-loose-v2"
            or "prepare-direct-delete-loose-v1"
            or "apply-direct-delete-loose-v1"
            or "verify-direct-delete-loose-v1"
             or "reconcile-apply-loose-v1"
            or "split-physical-loose-v1"
            or "plan-purge-loose-v2"
            or "purge-loose-v2"
            or "reconcile-empty-purge-loose-v2"
    )
    {
        await RunVideoVariantLooseMaintenance(operation, options);
        return;
    }

    if (operation is "preview-v2" or "preflight-v2" or "apply-v2" or "repair-entry-v2")
    {
        await RunVideoVariantMaintenanceV2(operation, options);
        return;
    }

    VideoVariantCleanupService service = new();

    if (operation == "benchmark")
    {
        string backupRoot = GetRequiredOption(options, "--backup-root");
        int chunkId = int.Parse(GetRequiredOption(options, "--chunk"));
        VideoVariantChunkBenchmark result = service.BenchmarkChunk(backupRoot, chunkId);
        Console.WriteLine($"chunk: {result.ChunkId}");
        Console.WriteLine($"json bytes: {result.JsonBytes}");
        Console.WriteLine($"read: {result.ReadTime.TotalMilliseconds:F1} ms");
        Console.WriteLine($"parse: {result.ParseTime.TotalMilliseconds:F1} ms");
        Console.WriteLine($"entries: {result.Entries}");
        Console.WriteLine($"mp4: {result.Mp4Entries}");
        Console.WriteLine($"groups: {result.Groups}");
        Console.WriteLine($"candidates: {result.Candidates}");
        Console.WriteLine($"sequential: {result.SequentialTime.TotalMilliseconds:F1} ms");
        Console.WriteLine($"parallel: {result.ParallelTime.TotalMilliseconds:F1} ms");
        return;
    }

    if (operation == "preview")
    {
        List<string> mediaRoots = GetOptions(options, "--media-root");
        string backupRoot = GetRequiredOption(options, "--backup-root");
        string output = GetRequiredOption(options, "--output");
        int parallelism = int.Parse(GetOption(options, "--parallel") ?? "8");
        VideoVariantCleanupManifest manifest = service.Preview(
            mediaRoots,
            backupRoot,
            includeDirect: !HasFlag(options, "--skip-direct"),
            includeChunks: !HasFlag(options, "--skip-chunks"),
            chunkParallelism: parallelism,
            progress: value =>
            {
                if (value.CompletedChunks % 10 != 0 && value.CompletedChunks != value.TotalChunks)
                    return;

                TimeSpan eta =
                    value.CompletedChunks == 0
                        ? TimeSpan.Zero
                        : value.Elapsed
                            / value.CompletedChunks
                            * (value.TotalChunks - value.CompletedChunks);
                Console.Error.WriteLine(
                    $"chunks: {value.CompletedChunks}/{value.TotalChunks}, entries: {value.Entries}, mp4: {value.Mp4Entries}, elapsed: {value.Elapsed:c}, ETA: {eta:c}, parallelism: {value.Parallelism}"
                );
                Console.Error.Flush();
            }
        );
        service.SaveManifest(manifest, output);

        long bytes = manifest
            .Candidates.SelectMany(candidate => candidate.Copies)
            .Sum(copy => copy.Size);
        Console.WriteLine($"manifest: {Path.GetFullPath(output)}");
        Console.WriteLine($"approval: {manifest.ApprovalHash}");
        Console.WriteLine($"variants: {manifest.Candidates.Count}");
        Console.WriteLine(
            $"copies: {manifest.Candidates.Sum(candidate => candidate.Copies.Count)}"
        );
        Console.WriteLine($"bytes: {bytes}");
        Console.WriteLine($"scan errors: {manifest.ScanErrors.Count}");
        return;
    }

    if (operation == "apply")
    {
        string manifestPath = GetRequiredOption(options, "--manifest");
        string approval = GetRequiredOption(options, "--approval");
        VideoVariantCleanupManifest manifest = service.LoadManifest(manifestPath);

        if (!approval.Equals(manifest.ApprovalHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Approval hash does not match manifest.");

        VideoVariantCleanupResult result = service.Apply(manifest);
        Console.WriteLine($"removed variants: {result.RemovedVariants}");
        Console.WriteLine($"removed copies: {result.RemovedCopies}");
        Console.WriteLine($"removed bytes: {result.RemovedBytes}");
        return;
    }

    throw new ArgumentException($"Unknown video variant maintenance operation: {operation}");
}

static async Task RunVideoVariantLooseMaintenance(string operation, string[] options)
{
    VideoVariantLooseCleanupPreviewService service = new();
    VideoVariantLooseCleanupApplyService applyService = new();
    VideoVariantLooseCleanupPurgeService purgeService = new();
    using CancellationTokenSource cts = new();
    void handler(object? _, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        cts.Cancel();
        Console.Error.WriteLine("[shutdown] cancellation requested (Ctrl+C)");
    }
    Console.CancelKeyPress += handler;
    try
    {
        Progress<VideoVariantLooseCleanupProgress> progress = new(value =>
        {
            string itemProgress =
                value.TotalItems > 0
                    ? $", items: {value.ItemsChecked}/{value.TotalItems}"
                    : string.Empty;
            string rootProgress = value.RootLabel is not null
                ? $", root {value.RootLabel}: {value.RootChecked}/{value.RootTotal}"
                : string.Empty;
            Console.Error.WriteLine(
                $"[{value.Phase}] cache rows: {value.CacheRowsScanned}, videos: {value.VideoEntries}{itemProgress}{rootProgress}, issues: {value.Issues}, elapsed: {value.Elapsed:c}"
            );
            Console.Error.Flush();
        });

        if (operation == "split-physical-loose-v1")
        {
            VideoVariantLooseCleanupSplitIndex index = new VideoVariantLooseCleanupManifestSplitService().Split(
                GetRequiredOption(options, "--manifest"),
                GetRequiredOption(options, "--output-dir"),
                int.Parse(GetOption(options, "--chunk-size") ?? "100000")
            );
            Console.WriteLine($"index: {Path.GetFullPath(Path.Combine(GetRequiredOption(options, "--output-dir"), "index.json"))}");
            Console.WriteLine($"source approval: {index.SourceApprovalHash}");
            Console.WriteLine($"chunks: {index.Total}");
            Console.WriteLine($"operations: {index.Operations}");
            Console.WriteLine($"bytes: {index.Bytes}");
            return;
        }

        if (operation == "preview-loose-v1")
        {
            string output = GetRequiredOption(options, "--output");
            int fileParallelism = int.Parse(GetOption(options, "--file-parallel") ?? "16");
            int? fileLimit = GetOption(options, "--file-limit") is string limit
                ? int.Parse(limit)
                : null;
            VideoVariantLooseCleanupManifest manifest = await service.PreviewAsync(
                GetRequiredOption(options, "--data-config"),
                GetRequiredOption(options, "--media-store"),
                GetRequiredOption(options, "--cache"),
                GetRequiredOption(options, "--direct-root"),
                fileParallelism,
                fileLimit,
                dbOnly: HasFlag(options, "--db-only"),
                selectionKind: GetOption(options, "--selection") ?? "video-variants",
                progress: progress,
                token: cts.Token
            );
            service.SaveManifest(manifest, output);
            Console.WriteLine($"manifest: {Path.GetFullPath(output)}");
            Console.WriteLine($"approval: {manifest.ApprovalHash}");
            Console.WriteLine($"cache rows: {manifest.CacheRowsScanned}");
            Console.WriteLine($"videos: {manifest.VideoEntries}");
            Console.WriteLine(
                $"selection: {GetOption(options, "--selection") ?? "video-variants"}"
            );
            Console.WriteLine($"file parallelism: {manifest.FileParallelism}");
            Console.WriteLine($"physical checked: {manifest.PhysicalChecked}");
            Console.WriteLine($"selection limit: {manifest.SelectionLimit?.ToString() ?? "none"}");
            Console.WriteLine($"operations: {manifest.Operations.Count}");
            Console.WriteLine($"issues: {manifest.Issues.Count}");
            return;
        }

        if (operation == "prepare-apply-loose-v2")
        {
            VideoVariantLooseCleanupApplyManifest manifest = await applyService.PrepareAsync(
                applyService.LoadSourceManifest(GetRequiredOption(options, "--manifest")),
                cts.Token
            );
            string output = GetRequiredOption(options, "--output");
            applyService.SaveManifest(manifest, output);
            Console.WriteLine($"manifest: {Path.GetFullPath(output)}");
            Console.WriteLine($"approval: {manifest.ApprovalHash}");
            Console.WriteLine($"operation set: {manifest.OperationSetHash}");
            Console.WriteLine($"operations: {manifest.Operations.Count}");
            Console.WriteLine($"incremental namespaces: {manifest.IncrementalNamespaces.Count}");
            return;
        }

        if (operation == "prepare-direct-delete-loose-v1")
        {
            VideoVariantLooseCleanupApplyManifest manifest = applyService.LoadManifest(
                GetRequiredOption(options, "--manifest")
            );
            VideoVariantLooseCleanupDirectDeletePlan plan =
                await applyService.PrepareDirectDeleteAsync(
                    manifest,
                    GetRequiredOption(options, "--source-journal"),
                    int.Parse(GetRequiredOption(options, "--max-operations")),
                    cts.Token
                );
            string output = GetRequiredOption(options, "--output");
            applyService.SaveDirectDeletePlan(plan, output);
            Console.WriteLine($"plan: {Path.GetFullPath(output)}");
            Console.WriteLine($"approval: {plan.ApprovalHash}");
            Console.WriteLine($"operations: {plan.Operations}");
            Console.WriteLine($"bytes: {plan.Bytes}");
            return;
        }

        if (operation is "apply-direct-delete-loose-v1" or "verify-direct-delete-loose-v1")
        {
            VideoVariantLooseCleanupApplyManifest manifest = applyService.LoadManifest(
                GetRequiredOption(options, "--manifest")
            );
            string sourceJournal = GetRequiredOption(options, "--source-journal");
            VideoVariantLooseCleanupDirectDeletePlan plan = applyService.LoadDirectDeletePlan(
                GetRequiredOption(options, "--plan")
            );
            if (operation == "verify-direct-delete-loose-v1")
            {
                await applyService.VerifyDirectDeleteAsync(
                    manifest,
                    sourceJournal,
                    plan,
                    GetRequiredOption(options, "--receipt"),
                    cts.Token
                );
                Console.WriteLine("verified: true");
                return;
            }
            Progress<VideoVariantLooseCleanupApplyProgress> directProgress = new(value =>
            {
                Console.Error.WriteLine(
                    $"batches: {value.CompletedBatches}/{value.TotalBatches}, batch operations: {value.BatchOperations}, total operations: {value.TotalOperations}"
                );
                Console.Error.Flush();
            });
            VideoVariantLooseCleanupDirectDeleteReceipt receipt =
                await applyService.ApplyDirectDeleteAsync(
                    manifest,
                    sourceJournal,
                    plan,
                    GetRequiredOption(options, "--approval"),
                    GetRequiredOption(options, "--journal"),
                    GetRequiredOption(options, "--receipt"),
                    int.Parse(GetRequiredOption(options, "--batch-size")),
                    HasFlag(options, "--resume"),
                    HasFlag(options, "--runtime-stopped-confirmed"),
                    int.Parse(GetRequiredOption(options, "--physical-parallel")),
                    directProgress,
                    cts.Token
                );
            Console.WriteLine($"operations: {receipt.Operations}");
            Console.WriteLine($"files deleted: {receipt.FilesDeleted}");
            Console.WriteLine($"bytes deleted: {receipt.BytesDeleted}");
            Console.WriteLine($"receipt hash: {receipt.ReceiptHash}");
            return;
        }

        if (operation == "reconcile-apply-loose-v1")
        {
            VideoVariantLooseCleanupApplyManifest manifest = applyService.LoadManifest(
                GetRequiredOption(options, "--manifest")
            );
            Progress<VideoVariantLooseCleanupApplyProgress> reconcileProgress = new(value =>
            {
                Console.Error.WriteLine(
                    $"batches: {value.CompletedBatches}/{value.TotalBatches}, batch operations: {value.BatchOperations}, total operations: {value.TotalOperations}"
                );
                Console.Error.Flush();
            });
            VideoVariantLooseCleanupReceipt receipt =
                await applyService.ReconcileLegacyJournalAsync(
                    manifest,
                    GetRequiredOption(options, "--source-journal"),
                    GetRequiredOption(options, "--approval"),
                    GetRequiredOption(options, "--journal"),
                    GetRequiredOption(options, "--receipt"),
                    HasFlag(options, "--resume"),
                    HasFlag(options, "--runtime-stopped-confirmed"),
                    int.Parse(GetRequiredOption(options, "--physical-parallel")),
                    reconcileProgress,
                    cts.Token
                );
            Console.WriteLine($"operations: {receipt.Operations}");
            Console.WriteLine($"originals quarantined: {receipt.OriginalsQuarantined}");
            Console.WriteLine($"direct quarantined: {receipt.DirectQuarantined}");
            Console.WriteLine($"bytes quarantined: {receipt.BytesQuarantined}");
            Console.WriteLine($"receipt hash: {receipt.ReceiptHash}");
            return;
        }

        if (
            operation
            is "check-loose-indexes-v2"
                or "create-loose-indexes-v2"
                or "apply-loose-v2"
                or "verify-loose-v2"
                or "plan-purge-loose-v2"
                or "purge-loose-v2"
                or "reconcile-empty-purge-loose-v2"
        )
        {
            VideoVariantLooseCleanupApplyManifest manifest = applyService.LoadManifest(
                GetRequiredOption(options, "--manifest")
            );
            if (operation is "plan-purge-loose-v2" or "purge-loose-v2" or "reconcile-empty-purge-loose-v2")
            {
                string applyReceipt = GetRequiredOption(options, "--receipt");
                string drive = GetRequiredOption(options, "--drive");
                VideoVariantLooseCleanupPurgePlan plan = purgeService.Plan(
                    manifest,
                    applyReceipt,
                    drive
                );
                Console.WriteLine($"drive: {plan.Drive}");
                Console.WriteLine($"quarantine root: {plan.QuarantineRoot}");
                Console.WriteLine($"files: {plan.Files}");
                Console.WriteLine($"bytes: {plan.Bytes}");
                Console.WriteLine($"apply receipt hash: {plan.ApplyReceiptHash}");
                if (operation == "plan-purge-loose-v2")
                    return;
                if (operation == "reconcile-empty-purge-loose-v2")
                {
                    VideoVariantLooseCleanupPurgeReceipt reconciled =
                        purgeService.ReconcileEmpty(
                            manifest,
                            applyReceipt,
                            drive,
                            GetRequiredOption(options, "--approval"),
                            GetRequiredOption(options, "--journal"),
                            GetRequiredOption(options, "--purge-receipt"),
                            HasFlag(options, "--runtime-stopped-confirmed")
                        );
                    Console.WriteLine($"files deleted: {reconciled.FilesDeleted}");
                    Console.WriteLine($"bytes deleted: {reconciled.BytesDeleted}");
                    Console.WriteLine($"files already absent: {reconciled.FilesAlreadyAbsent}");
                    Console.WriteLine($"bytes already absent: {reconciled.BytesAlreadyAbsent}");
                    Console.WriteLine($"purge receipt hash: {reconciled.ReceiptHash}");
                    return;
                }
                Progress<VideoVariantLooseCleanupPurgeProgress> purgeProgress = new(value =>
                {
                    double percent =
                        value.TotalFiles == 0 ? 100 : value.FilesDeleted * 100d / value.TotalFiles;
                    Console.Error.WriteLine(
                        $"drive: {plan.Drive}, batches: {value.CompletedBatches}/{value.TotalBatches}, files: {value.FilesDeleted}/{value.TotalFiles}, bytes: {value.BytesDeleted}/{value.TotalBytes}, percent: {percent:F2}"
                    );
                    Console.Error.Flush();
                });
                VideoVariantLooseCleanupPurgeReceipt purgeReceipt = await purgeService.PurgeAsync(
                    manifest,
                    applyReceipt,
                    drive,
                    GetRequiredOption(options, "--approval"),
                    GetRequiredOption(options, "--journal"),
                    GetRequiredOption(options, "--purge-receipt"),
                    int.Parse(GetOption(options, "--batch-size") ?? "1000"),
                    HasFlag(options, "--resume"),
                    HasFlag(options, "--runtime-stopped-confirmed"),
                    purgeProgress,
                    cts.Token
                );
                Console.WriteLine($"files deleted: {purgeReceipt.FilesDeleted}");
                Console.WriteLine($"bytes deleted: {purgeReceipt.BytesDeleted}");
                Console.WriteLine($"purge receipt hash: {purgeReceipt.ReceiptHash}");
                return;
            }
            if (operation == "check-loose-indexes-v2")
            {
                VideoVariantLooseCleanupIndexStatus status = await applyService.CheckIndexesAsync(
                    manifest,
                    cts.Token
                );
                Console.WriteLine($"primary valid: {status.PrimaryValid}");
                Console.WriteLine($"incremental valid: {status.IncrementalValid}");
                if (!status.IsValid)
                    Environment.ExitCode = 1;
                return;
            }
            if (operation == "create-loose-indexes-v2")
            {
                VideoVariantLooseCleanupIndexStatus status = await applyService.CreateIndexesAsync(
                    manifest,
                    GetRequiredOption(options, "--approval"),
                    cts.Token
                );
                Console.WriteLine($"primary valid: {status.PrimaryValid}");
                Console.WriteLine($"incremental valid: {status.IncrementalValid}");
                return;
            }
            Progress<VideoVariantLooseCleanupApplyProgress> applyProgress = new(value =>
            {
                Console.Error.WriteLine(
                    $"batches: {value.CompletedBatches}/{value.TotalBatches}, batch operations: {value.BatchOperations}, total operations: {value.TotalOperations}"
                );
                Console.Error.Flush();
            });
            if (operation == "apply-loose-v2")
            {
                VideoVariantLooseCleanupReceipt receipt = await applyService.ApplyParallelAsync(
                    manifest,
                    GetRequiredOption(options, "--approval"),
                    GetRequiredOption(options, "--journal"),
                    GetRequiredOption(options, "--receipt"),
                    int.Parse(GetOption(options, "--batch-size") ?? "1000"),
                    HasFlag(options, "--resume"),
                    HasFlag(options, "--runtime-stopped-confirmed"),
                    int.Parse(GetOption(options, "--physical-parallel") ?? "1"),
                    applyProgress,
                    cts.Token
                );
                Console.WriteLine($"operations: {receipt.Operations}");
                Console.WriteLine($"originals quarantined: {receipt.OriginalsQuarantined}");
                Console.WriteLine($"direct quarantined: {receipt.DirectQuarantined}");
                Console.WriteLine($"bytes quarantined: {receipt.BytesQuarantined}");
                Console.WriteLine($"receipt hash: {receipt.ReceiptHash}");
                return;
            }
            await applyService.VerifyAsync(
                manifest,
                GetRequiredOption(options, "--receipt"),
                applyProgress,
                cts.Token
            );
            Console.WriteLine("verified: true");
            return;
        }

        string preflightOutput = GetRequiredOption(options, "--output");
        VideoVariantLooseCleanupManifest loaded = service.LoadManifest(
            GetRequiredOption(options, "--manifest")
        );
        VideoVariantLooseCleanupPreflightResult result = await service.PreflightAsync(
            loaded,
            progress: progress,
            token: cts.Token
        );
        Console.WriteLine($"operations: {result.Operations}");
        Console.WriteLine($"original files: {result.OriginalFiles}");
        Console.WriteLine($"original bytes: {result.OriginalBytes}");
        Console.WriteLine($"direct files: {result.DirectFiles}");
        Console.WriteLine($"direct bytes: {result.DirectBytes}");
        Console.WriteLine($"errors: {result.Errors.Count}");
        if (result.IsValid && result.PhysicalManifest is not null)
        {
            service.SaveManifest(result.PhysicalManifest, preflightOutput);
            Console.WriteLine($"manifest: {Path.GetFullPath(preflightOutput)}");
            Console.WriteLine($"approval: {result.PhysicalManifest.ApprovalHash}");
            Console.WriteLine($"issues: {result.PhysicalManifest.Issues.Count}");
            Console.WriteLine($"physical checked: {result.PhysicalManifest.PhysicalChecked}");
        }

        foreach (string error in result.Errors.Take(100))
            Console.Error.WriteLine(error);
        if (result.Errors.Count > 100)
            Console.Error.WriteLine($"{result.Errors.Count - 100} additional errors omitted.");
        if (!result.IsValid)
            Environment.ExitCode = 1;
    }
    finally
    {
        Console.CancelKeyPress -= handler;
    }
}

static async Task RunVideoVariantMaintenanceV2(string operation, string[] options)
{
    VideoVariantChunkCleanupV2Service service = new();
    using CancellationTokenSource cts = new();
    void handler(object? _, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        cts.Cancel();
    }
    Console.CancelKeyPress += handler;

    try
    {
        if (operation == "repair-entry-v2")
        {
            VideoVariantChunkCleanupV2RepairResult result = await service.RepairEntryFromEntryAsync(
                GetRequiredOption(options, "--backup-root"),
                int.Parse(GetRequiredOption(options, "--chunk")),
                GetRequiredOption(options, "--source"),
                GetRequiredOption(options, "--target"),
                GetRequiredOption(options, "--sha256"),
                cts.Token
            );
            Console.WriteLine($"chunk: {result.ChunkId}");
            Console.WriteLine($"target: {result.TargetPath}");
            Console.WriteLine($"size: {result.FileSize}");
            Console.WriteLine($"crc32: {result.Crc32}");
            Console.WriteLine($"sha256: {result.Hash}");
            return;
        }

        if (operation == "preview-v2")
        {
            string backupRoot = GetRequiredOption(options, "--backup-root");
            string output = GetRequiredOption(options, "--output");
            int parallelism = int.Parse(GetOption(options, "--parallel") ?? "8");
            Progress<VideoVariantChunkCleanupV2PreviewProgress> progress = new(value =>
            {
                if (value.CompletedChunks % 10 != 0 && value.CompletedChunks != value.TotalChunks)
                    return;

                Console.Error.WriteLine(
                    $"chunks: {value.CompletedChunks}/{value.TotalChunks}, entries: {value.Entries}, videos: {value.VideoEntries}, elapsed: {value.Elapsed:c}, parallelism: {value.Parallelism}"
                );
                Console.Error.Flush();
            });
            VideoVariantChunkCleanupV2Manifest manifest = await service.PreviewAsync(
                backupRoot,
                parallelism,
                progress,
                cts.Token
            );
            service.SaveManifest(manifest, output);
            Console.WriteLine($"manifest: {Path.GetFullPath(output)}");
            Console.WriteLine($"approval: {manifest.ApprovalHash}");
            Console.WriteLine($"variants: {manifest.Variants.Count}");
            Console.WriteLine($"chunks: {manifest.Chunks.Count}");
            Console.WriteLine(
                $"removals: {manifest.Chunks.Sum(chunk => chunk.RemovePathIndices.Count)}"
            );
            return;
        }

        string manifestPath = GetRequiredOption(options, "--manifest");
        VideoVariantChunkCleanupV2Manifest loaded = service.LoadManifest(manifestPath);

        if (operation == "preflight-v2")
        {
            int parallelism = int.Parse(GetOption(options, "--parallel") ?? "8");
            Progress<VideoVariantChunkCleanupV2PreflightProgress> progress = new(value =>
            {
                if (value.CompletedChunks % 10 != 0 && value.CompletedChunks != value.TotalChunks)
                    return;

                Console.Error.WriteLine(
                    $"chunks: {value.CompletedChunks}/{value.TotalChunks}, entries: {value.Entries}, removals: {value.Removals}, errors: {value.Errors}, elapsed: {value.Elapsed:c}, parallelism: {value.Parallelism}"
                );
                Console.Error.Flush();
            });
            VideoVariantChunkCleanupV2PreflightResult result = await service.PreflightAsync(
                loaded,
                parallelism,
                progress,
                cts.Token
            );
            Console.WriteLine($"chunks: {result.Chunks}");
            Console.WriteLine($"entries: {result.Entries}");
            Console.WriteLine($"removals: {result.Removals}");
            Console.WriteLine($"bytes: {result.Bytes}");
            Console.WriteLine($"errors: {result.Errors.Count}");

            foreach (string error in result.Errors)
                Console.Error.WriteLine(error);

            if (!result.IsValid)
                Environment.ExitCode = 1;
            return;
        }

        if (operation == "apply-v2")
        {
            string approval = GetRequiredOption(options, "--approval");
            Progress<VideoVariantChunkCleanupV2ApplyProgress> progress = new(value =>
            {
                Console.Error.WriteLine(
                    $"chunks: {value.CommittedChunks}/{value.TotalChunks}, current: {value.CurrentChunkId}, removals: {value.RemovedEntries}, bytes: {value.RemovedBytes}"
                );
                Console.Error.Flush();
            });
            VideoVariantChunkCleanupV2ApplyResult result = await service.ApplyAsync(
                loaded,
                approval,
                resume: HasFlag(options, "--resume"),
                skipChunkIds: GetOptions(options, "--skip-chunk").Select(int.Parse).ToHashSet(),
                progress: progress,
                token: cts.Token
            );
            Console.WriteLine($"chunks: {result.Chunks}");
            Console.WriteLine($"removals: {result.Removals}");
            Console.WriteLine($"bytes: {result.Bytes}");
            Console.WriteLine($"skipped chunks: {result.SkippedChunks}");
            return;
        }

        throw new ArgumentException($"Unknown video variant maintenance operation: {operation}");
    }
    finally
    {
        Console.CancelKeyPress -= handler;
    }
}

static string GetRequiredOption(string[] args, string name) =>
    GetOption(args, name) ?? throw new ArgumentException($"Missing required option {name}.");

static string? GetOption(string[] args, string name) => GetOptions(args, name).SingleOrDefault();

static List<string> GetOptions(string[] args, string name)
{
    List<string> values = [];

    for (int index = 0; index < args.Length; index++)
    {
        if (args[index] != name)
            continue;

        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Missing value for option {name}.");

        values.Add(args[++index]);
    }

    return values;
}

static bool HasFlag(string[] args, string name) => args.Contains(name, StringComparer.Ordinal);
