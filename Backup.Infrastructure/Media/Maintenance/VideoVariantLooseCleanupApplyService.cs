using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using NpgsqlTypes;

namespace Backup.Infrastructure.Media.Maintenance;

public sealed class VideoVariantLooseCleanupApplyService
{
    internal const string PrimaryIndexName = "ix_media_cache_primary_namespace_normalized_path";
    internal const string IncrementalIndexName =
        "ix_media_cache_incremental_namespace_normalized_path";
    internal const string CreatePrimaryIndexSql =
        "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_media_cache_primary_namespace_normalized_path ON media_cache_primary_entries (namespace, (lower(replace(path, chr(92), '/'))))";
    internal const string CreateIncrementalIndexSql =
        "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_media_cache_incremental_namespace_normalized_path ON media_cache_incremental_entries (namespace, (lower(replace(path, chr(92), '/'))))";
    internal const string DropInvalidPrimaryIndexSql =
        "DROP INDEX CONCURRENTLY IF EXISTS ix_media_cache_primary_namespace_normalized_path";
    internal const string DropInvalidIncrementalIndexSql =
        "DROP INDEX CONCURRENTLY IF EXISTS ix_media_cache_incremental_namespace_normalized_path";
    private const string QuarantineDirectory = ".video-variant-loose-quarantine";
    private const string LockFileName = ".video-variant-loose-cleanup.lock";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };
    private readonly IVideoVariantLooseCleanupDatabase? _database;
    private readonly ConcurrentDictionary<string, Lazy<bool>> _createdDirectories = new(
        PhysicalPathSemantics.Comparer
    );

    public VideoVariantLooseCleanupApplyService() { }

    internal VideoVariantLooseCleanupApplyService(IVideoVariantLooseCleanupDatabase database) =>
        _database = database;

    internal Action<string>? FaultInjection { get; init; }

    public async Task<VideoVariantLooseCleanupApplyManifest> PrepareAsync(
        VideoVariantLooseCleanupManifest source,
        CancellationToken token = default
    )
    {
        ValidateSource(source);
        ResolvedConfig config = VideoVariantLooseCleanupPreviewService.LoadConfig(
            source.DataConfigPath,
            source.MediaStoreId,
            source.CacheId
        );
        IVideoVariantLooseCleanupDatabase database = Database(config.ConnectionString);
        VideoVariantLooseCleanupNamespaces namespaces = await database.DiscoverNamespacesAsync(
            StoreNamespace(source.MediaStoreId, source.CacheId),
            token
        );
        if (!namespaces.PrimaryNamespace.Equals(source.CacheNamespace, StringComparison.Ordinal))
            throw new InvalidOperationException("Primary cache namespace changed.");

        string[] keepPaths = source
            .Operations.Select(operation => NormalizePath(source.Paths[operation.KeepPathIndex]))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        IReadOnlyDictionary<string, int> keepPartitionIds = await database.LoadPartitionIdsAsync(
            namespaces.PrimaryNamespace,
            keepPaths,
            token
        );

        byte[] configBytes = await File.ReadAllBytesAsync(config.DataConfigPath, token);
        VideoVariantLooseCleanupApplyManifest manifest = new()
        {
            Version = 3,
            CreatedUtc = DateTimeOffset.UtcNow,
            SourceManifestApprovalHash = source.ApprovalHash,
            DataConfigPath = config.DataConfigPath,
            DataConfigLength = config.DataConfigLength,
            DataConfigLastWriteUtcTicks = config.DataConfigLastWriteUtcTicks,
            DataConfigSha256 = Sha256(configBytes),
            MediaStoreId = source.MediaStoreId,
            CacheId = source.CacheId,
            PrimaryNamespace = namespaces.PrimaryNamespace,
            IncrementalNamespaces = namespaces
                .IncrementalNamespaces.Order(StringComparer.Ordinal)
                .ToList(),
            DirectRoot = Path.GetFullPath(source.DirectRoot),
            Paths = [.. source.Paths.Order(StringComparer.Ordinal)],
        };
        Dictionary<string, int> pathIndices = manifest
            .Paths.Select((path, index) => (path, index))
            .ToDictionary(value => value.path, value => value.index, StringComparer.Ordinal);
        manifest.Operations = source
            .Operations.Select(operation => new VideoVariantLooseCleanupApplyOperation
            {
                RemovePathIndex = pathIndices[source.Paths[operation.RemovePathIndex]],
                KeepPathIndex = pathIndices[source.Paths[operation.KeepPathIndex]],
                PartitionId = operation.PartitionId,
                KeepPartitionId = keepPartitionIds[
                    NormalizePath(source.Paths[operation.KeepPathIndex])
                ],
                OriginalSize = operation.OriginalSize,
                OriginalLastWriteUtcTicks = operation.OriginalLastWriteUtcTicks,
                DirectPresent = operation.DirectPresent,
                DirectSize = operation.DirectSize,
                DirectLastWriteUtcTicks = operation.DirectLastWriteUtcTicks,
            })
            .OrderBy(operation => operation.PartitionId)
            .ThenBy(operation => manifest.Paths[operation.RemovePathIndex], StringComparer.Ordinal)
            .ToList();
        manifest.OperationSetHash = CalculateOperationSetHash(manifest);
        manifest.ApprovalHash = CalculateApprovalHash(manifest);
        ValidateManifest(manifest, config.MediaRoots, verifyHash: true);
        return manifest;
    }

    public VideoVariantLooseCleanupManifest LoadSourceManifest(string path) =>
        new VideoVariantLooseCleanupPreviewService().LoadManifest(path);

    public VideoVariantLooseCleanupApplyManifest LoadManifest(string path)
    {
        VideoVariantLooseCleanupApplyManifest manifest =
            JsonSerializer.Deserialize<VideoVariantLooseCleanupApplyManifest>(
                File.ReadAllBytes(path),
                JsonOptions
            ) ?? throw new InvalidOperationException("Invalid loose cleanup apply manifest.");
        ValidateManifestStructure(manifest, verifyHash: true);
        return manifest;
    }

    public void SaveManifest(VideoVariantLooseCleanupApplyManifest manifest, string path)
    {
        ValidateManifestStructure(manifest, verifyHash: true);
        WriteJsonAtomic(path, manifest);
    }

    public async Task<VideoVariantLooseCleanupDirectDeletePlan> PrepareDirectDeleteAsync(
        VideoVariantLooseCleanupApplyManifest manifest,
        string sourceJournalPath,
        int maxOperations,
        CancellationToken token = default
    )
    {
        if (maxOperations <= 0 || maxOperations > 10_000)
            throw new ArgumentOutOfRangeException(nameof(maxOperations));
        ResolvedConfig config = ValidateCurrentConfig(manifest);
        List<ApplyWork> ordered = BuildWork(manifest, config.MediaRoots);
        byte[] journalBytes = await File.ReadAllBytesAsync(sourceJournalPath, token);
        VideoVariantLooseCleanupJournal sourceJournal = LoadAndValidateSourceJournal(
            manifest,
            journalBytes,
            ordered
        );
        int[] selectedIndices = sourceJournal
            .Batches.Where(batch => batch.State == "pending")
            .SelectMany(batch =>
                Enumerable.Range(
                    batch.Index * sourceJournal.BatchSize,
                    Math.Min(
                        sourceJournal.BatchSize,
                        ordered.Count - batch.Index * sourceJournal.BatchSize
                    )
                )
            )
            .Take(maxOperations)
            .ToArray();
        if (selectedIndices.Length == 0)
            throw new InvalidOperationException("Source journal has no pending operations.");

        ApplyWork[] selected = selectedIndices.Select(index => ordered[index]).ToArray();
        ValidateDirectDeleteFiles(
            selected,
            requireActiveFiles: true,
            physicalParallelism: 1,
            token
        );
        List<VideoVariantLooseCleanupDirectDeleteKeep> keeps = selected
            .Select(work => work.KeepPath)
            .Distinct(PhysicalPathSemantics.Comparer)
            .Order(PhysicalPathSemantics.Comparer)
            .Select(path =>
            {
                FileInfo info = new(path);
                if (!info.Exists)
                    throw new InvalidOperationException($"Keep file is absent: {path}");
                return new VideoVariantLooseCleanupDirectDeleteKeep
                {
                    Path = path,
                    Size = info.Length,
                    LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks,
                };
            })
            .ToList();
        IVideoVariantLooseCleanupDatabase database = Database(config.ConnectionString);
        VideoVariantLooseCleanupNamespaces currentNamespaces =
            await database.DiscoverNamespacesAsync(
                StoreNamespace(manifest.MediaStoreId, manifest.CacheId),
                token
            );
        ValidateNamespaces(manifest, currentNamespaces);
        await RequireExactRowCounts(database, manifest, selected, expected: 1, token);

        VideoVariantLooseCleanupDirectDeletePlan plan = new()
        {
            Version = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
            SourceManifestApprovalHash = manifest.ApprovalHash,
            SourceOperationSetHash = manifest.OperationSetHash,
            SourceJournalSha256 = Sha256(journalBytes),
            SourceJournalRunId = sourceJournal.RunId,
            SourceJournalBatchSize = sourceJournal.BatchSize,
            SourceJournalBatchCount = sourceJournal.Batches.Count,
            DataConfigSha256 = manifest.DataConfigSha256,
            OperationIndices = [.. selectedIndices],
            Keeps = keeps,
            Operations = selected.Length,
            Files = selected.Sum(work => 1 + (work.Operation.DirectPresent ? 1 : 0)),
            Bytes = selected.Sum(work =>
                work.Operation.OriginalSize
                + (work.Operation.DirectPresent ? work.Operation.DirectSize : 0)
            ),
        };
        plan.ApprovalHash = CalculateDirectDeletePlanHash(plan);
        return plan;
    }

    public void SaveDirectDeletePlan(VideoVariantLooseCleanupDirectDeletePlan plan, string path)
    {
        ValidateDirectDeletePlanStructure(plan);
        WriteJsonAtomic(path, plan);
    }

    public VideoVariantLooseCleanupDirectDeletePlan LoadDirectDeletePlan(string path)
    {
        VideoVariantLooseCleanupDirectDeletePlan plan =
            JsonSerializer.Deserialize<VideoVariantLooseCleanupDirectDeletePlan>(
                File.ReadAllBytes(path),
                JsonOptions
            ) ?? throw new InvalidOperationException("Invalid direct-delete plan.");
        ValidateDirectDeletePlanStructure(plan);
        return plan;
    }

    public async Task<VideoVariantLooseCleanupDirectDeleteReceipt> ApplyDirectDeleteAsync(
        VideoVariantLooseCleanupApplyManifest manifest,
        string sourceJournalPath,
        VideoVariantLooseCleanupDirectDeletePlan plan,
        string approval,
        string journalPath,
        string receiptPath,
        int batchSize,
        bool resume,
        bool runtimeStoppedConfirmed,
        int physicalParallelism,
        IProgress<VideoVariantLooseCleanupApplyProgress>? progress = null,
        CancellationToken token = default
    )
    {
        if (!runtimeStoppedConfirmed)
            throw new InvalidOperationException("Runtime stopped confirmation is required.");
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (physicalParallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(physicalParallelism));
        ValidateDirectDeletePlan(manifest, plan, approval);
        ResolvedConfig config = ValidateCurrentConfig(manifest);
        byte[] sourceJournalBytes = await File.ReadAllBytesAsync(sourceJournalPath, token);
        List<ApplyWork> allWork = BuildWork(manifest, config.MediaRoots);
        ValidateDirectDeleteSourceJournal(manifest, plan, sourceJournalBytes, allWork);
        ApplyWork[] selected = plan.OperationIndices.Select(index => allWork[index]).ToArray();
        if (
            selected.Length != plan.Operations
            || selected.Sum(work => 1 + (work.Operation.DirectPresent ? 1 : 0)) != plan.Files
            || selected.Sum(work =>
                work.Operation.OriginalSize
                + (work.Operation.DirectPresent ? work.Operation.DirectSize : 0)
            ) != plan.Bytes
        )
            throw new InvalidOperationException("Direct-delete plan operation totals are invalid.");
        string fullJournalPath = RequireLocalOutputPath(journalPath, nameof(journalPath));
        string fullReceiptPath = RequireLocalOutputPath(receiptPath, nameof(receiptPath));

        IVideoVariantLooseCleanupDatabase database = Database(config.ConnectionString);
        VideoVariantLooseCleanupNamespaces currentNamespaces =
            await database.DiscoverNamespacesAsync(
                StoreNamespace(manifest.MediaStoreId, manifest.CacheId),
                token
            );
        ValidateNamespaces(manifest, currentNamespaces);
        if (!(await database.CheckIndexesAsync(token)).IsValid)
            throw new InvalidOperationException(
                "Required normalized path indexes are missing or invalid."
            );

        List<FileStream> fileLocks = AcquireFileLocks(
            config.MediaRoots.Values.Append(manifest.DirectRoot)
        );
        try
        {
            await using IAsyncDisposable? advisoryLock = await database.TryAcquireLockAsync(
                StoreNamespace(manifest.MediaStoreId, manifest.CacheId),
                token
            );
            if (advisoryLock is null)
                throw new InvalidOperationException("Media cache advisory lock is unavailable.");
            VideoVariantLooseCleanupNamespaces lockedNamespaces =
                await database.DiscoverNamespacesAsync(
                    StoreNamespace(manifest.MediaStoreId, manifest.CacheId),
                    token
                );
            ValidateNamespaces(manifest, lockedNamespaces);
            ValidateDirectDeleteKeeps(plan, token);
            if (File.Exists(fullReceiptPath))
            {
                VideoVariantLooseCleanupDirectDeleteReceipt completed = LoadDirectDeleteReceipt(
                    fullReceiptPath
                );
                ValidateDirectDeleteReceipt(manifest, plan, completed);
                await VerifyDirectDeleteAsync(
                    manifest,
                    sourceJournalPath,
                    plan,
                    fullReceiptPath,
                    token
                );
                return completed;
            }
            int batchCount = (selected.Length + batchSize - 1) / batchSize;
            VideoVariantLooseCleanupDirectDeleteJournal journal = LoadOrCreateDirectDeleteJournal(
                plan,
                fullJournalPath,
                batchSize,
                batchCount,
                resume,
                selected
            );

            for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                VideoVariantLooseCleanupDirectDeleteJournalBatch state = journal.Batches[
                    batchIndex
                ];
                ApplyWork[] batch = selected.Skip(batchIndex * batchSize).Take(batchSize).ToArray();
                if (state.State == "verified")
                {
                    VerifyDirectDeleteFiles(batch, physicalParallelism, token);
                    await RequireExactRowCounts(database, manifest, batch, expected: 0, token);
                    continue;
                }
                token.ThrowIfCancellationRequested();

                if (state.State == "pending")
                {
                    ValidateDirectDeleteFiles(
                        batch,
                        requireActiveFiles: true,
                        physicalParallelism,
                        token
                    );
                    await RequireExactRowCounts(database, manifest, batch, expected: 1, token);
                    state.State = "deleting-files";
                    WriteDirectDeleteJournal(fullJournalPath, journal, selected);
                }
                if (state.State == "deleting-files")
                {
                    ValidateDirectDeleteFiles(
                        batch,
                        requireActiveFiles: false,
                        physicalParallelism,
                        token
                    );
                    await RequireExactRowCounts(database, manifest, batch, expected: 1, token);
                    DeleteDirectDeleteFiles(batch, physicalParallelism, token);
                    FaultInjection?.Invoke("after-direct-delete-files");
                    state.State = "files-absent";
                    state.FilesDeleted = batch.Sum(work =>
                        1 + (work.Operation.DirectPresent ? 1 : 0)
                    );
                    state.BytesDeleted = batch.Sum(work =>
                        work.Operation.OriginalSize
                        + (work.Operation.DirectPresent ? work.Operation.DirectSize : 0)
                    );
                    WriteDirectDeleteJournal(fullJournalPath, journal, selected);
                }
                if (state.State == "files-absent")
                {
                    VerifyDirectDeleteFiles(batch, physicalParallelism, token);
                    await RequireExactRowCounts(database, manifest, batch, expected: 1, token);
                    state.State = "db-deleting";
                    WriteDirectDeleteJournal(fullJournalPath, journal, selected);
                }
                if (state.State == "db-deleting")
                {
                    VerifyDirectDeleteFiles(batch, physicalParallelism, token);
                    IReadOnlyDictionary<string, int> counts = await LoadRowCounts(
                        database,
                        manifest,
                        batch,
                        token
                    );
                    if (counts.Values.All(count => count == 1))
                    {
                        VideoVariantLooseCleanupDeleteResult deleted =
                            await database.DeleteBatchAsync(
                                manifest.PrimaryNamespace,
                                manifest.IncrementalNamespaces,
                                batch.Select(work => NormalizePath(work.LogicalPath)).ToArray(),
                                token
                            );
                        if (deleted.PrimaryDeleted + deleted.IncrementalDeleted != batch.Length)
                            throw new InvalidOperationException(
                                "Direct-delete database delete count mismatch."
                            );
                        state.RowsDeleted = batch.Length;
                    }
                    else if (!counts.Values.All(count => count == 0))
                        throw new InvalidOperationException(
                            "Direct-delete database state is partial or duplicated."
                        );
                    if (
                        !await database.AreRowsAbsentAsync(
                            manifest.PrimaryNamespace,
                            manifest.IncrementalNamespaces,
                            batch.Select(work => NormalizePath(work.LogicalPath)).ToArray(),
                            token
                        )
                    )
                        throw new InvalidOperationException(
                            "Target cache rows remain after delete."
                        );
                    state.State = "db-committed";
                    WriteDirectDeleteJournal(fullJournalPath, journal, selected);
                    FaultInjection?.Invoke("after-direct-delete-db-commit");
                }
                if (state.State == "db-committed")
                {
                    VerifyDirectDeleteFiles(batch, physicalParallelism, token);
                    await RequireExactRowCounts(database, manifest, batch, expected: 0, token);
                    state.State = "verified";
                    WriteDirectDeleteJournal(fullJournalPath, journal, selected);
                }
                progress?.Report(new(batchIndex + 1, batchCount, batch.Length, selected.Length));
            }

            VideoVariantLooseCleanupDirectDeleteReceipt receipt = new()
            {
                Version = 1,
                PlanApprovalHash = plan.ApprovalHash,
                SourceManifestApprovalHash = manifest.ApprovalHash,
                SourceOperationSetHash = manifest.OperationSetHash,
                SourceJournalSha256 = plan.SourceJournalSha256,
                SourceJournalRunId = plan.SourceJournalRunId,
                SourceJournalBatchSize = plan.SourceJournalBatchSize,
                SourceJournalBatchCount = plan.SourceJournalBatchCount,
                RunId = journal.RunId,
                StartedUtc = journal.StartedUtc,
                CompletedUtc = DateTimeOffset.UtcNow,
                Operations = plan.Operations,
                FilesDeleted = plan.Files,
                BytesDeleted = plan.Bytes,
                RowsDeleted = plan.Operations,
                Batches = batchCount,
            };
            receipt.ReceiptHash = CalculateDirectDeleteReceiptHash(receipt);
            WriteJsonAtomic(fullReceiptPath, receipt);
            return receipt;
        }
        finally
        {
            DisposeLocks(fileLocks);
        }
    }

    public async Task VerifyDirectDeleteAsync(
        VideoVariantLooseCleanupApplyManifest manifest,
        string sourceJournalPath,
        VideoVariantLooseCleanupDirectDeletePlan plan,
        string receiptPath,
        CancellationToken token = default
    )
    {
        ValidateDirectDeletePlan(manifest, plan, plan.ApprovalHash);
        ResolvedConfig config = ValidateCurrentConfig(manifest);
        byte[] sourceJournalBytes = await File.ReadAllBytesAsync(sourceJournalPath, token);
        List<ApplyWork> allWork = BuildWork(manifest, config.MediaRoots);
        ValidateDirectDeleteSourceJournal(manifest, plan, sourceJournalBytes, allWork);
        VideoVariantLooseCleanupDirectDeleteReceipt receipt = LoadDirectDeleteReceipt(receiptPath);
        ValidateDirectDeleteReceipt(manifest, plan, receipt);
        ApplyWork[] selected = plan.OperationIndices.Select(index => allWork[index]).ToArray();
        ValidateDirectDeleteKeeps(plan, token);
        VerifyDirectDeleteFiles(selected, 1, token);
        IVideoVariantLooseCleanupDatabase database = Database(config.ConnectionString);
        VideoVariantLooseCleanupNamespaces currentNamespaces =
            await database.DiscoverNamespacesAsync(
                StoreNamespace(manifest.MediaStoreId, manifest.CacheId),
                token
            );
        ValidateNamespaces(manifest, currentNamespaces);
        await RequireExactRowCounts(database, manifest, selected, 0, token);
    }

    public async Task<VideoVariantLooseCleanupIndexStatus> CheckIndexesAsync(
        VideoVariantLooseCleanupApplyManifest manifest,
        CancellationToken token = default
    )
    {
        ResolvedConfig config = ValidateCurrentConfig(manifest);
        return await Database(config.ConnectionString).CheckIndexesAsync(token);
    }

    public async Task<VideoVariantLooseCleanupIndexStatus> CreateIndexesAsync(
        VideoVariantLooseCleanupApplyManifest manifest,
        string approval,
        CancellationToken token = default
    )
    {
        ValidateApproval(manifest, approval);
        ResolvedConfig config = ValidateCurrentConfig(manifest);
        IVideoVariantLooseCleanupDatabase database = Database(config.ConnectionString);
        await database.CreateIndexesAsync(token);
        VideoVariantLooseCleanupIndexStatus status = await database.CheckIndexesAsync(token);
        if (!status.IsValid)
            throw new InvalidOperationException("Required normalized path indexes are not valid.");
        return status;
    }

    public Task<VideoVariantLooseCleanupReceipt> ApplyAsync(
        VideoVariantLooseCleanupApplyManifest manifest,
        string approval,
        string journalPath,
        string receiptPath,
        int batchSize = 1000,
        bool resume = false,
        bool runtimeStoppedConfirmed = false,
        IProgress<VideoVariantLooseCleanupApplyProgress>? progress = null,
        CancellationToken token = default
    ) =>
        ApplyParallelAsync(
            manifest,
            approval,
            journalPath,
            receiptPath,
            batchSize,
            resume,
            runtimeStoppedConfirmed,
            1,
            progress,
            token
        );

    public async Task<VideoVariantLooseCleanupReceipt> ApplyParallelAsync(
        VideoVariantLooseCleanupApplyManifest manifest,
        string approval,
        string journalPath,
        string receiptPath,
        int batchSize,
        bool resume,
        bool runtimeStoppedConfirmed,
        int physicalParallelism,
        IProgress<VideoVariantLooseCleanupApplyProgress>? progress = null,
        CancellationToken token = default
    )
    {
        if (!runtimeStoppedConfirmed)
            throw new InvalidOperationException("Runtime stopped confirmation is required.");
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (physicalParallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(physicalParallelism));
        ValidateApproval(manifest, approval);
        ResolvedConfig config = ValidateCurrentConfig(manifest);
        ValidateManifest(manifest, config.MediaRoots, verifyHash: true);
        string fullJournalPath = RequireLocalOutputPath(journalPath, nameof(journalPath));
        string fullReceiptPath = RequireLocalOutputPath(receiptPath, nameof(receiptPath));

        if (File.Exists(fullReceiptPath))
        {
            VideoVariantLooseCleanupReceipt completed = LoadReceipt(fullReceiptPath);
            if (
                completed.ApprovalHash == manifest.ApprovalHash
                && completed.OperationSetHash == manifest.OperationSetHash
            )
                return completed;
            throw new InvalidOperationException(
                "Receipt path already contains a different receipt."
            );
        }

        IVideoVariantLooseCleanupDatabase database = Database(config.ConnectionString);
        VideoVariantLooseCleanupNamespaces currentNamespaces =
            await database.DiscoverNamespacesAsync(
                StoreNamespace(manifest.MediaStoreId, manifest.CacheId),
                token
            );
        ValidateNamespaces(manifest, currentNamespaces);
        VideoVariantLooseCleanupIndexStatus indexes = await database.CheckIndexesAsync(token);
        if (!indexes.IsValid)
            throw new InvalidOperationException(
                "Required normalized path indexes are missing or invalid."
            );

        List<FileStream> fileLocks = AcquireFileLocks(
            config.MediaRoots.Values.Append(manifest.DirectRoot)
        );
        await using IAsyncDisposable? advisoryLock = await database.TryAcquireLockAsync(
            StoreNamespace(manifest.MediaStoreId, manifest.CacheId),
            token
        );
        if (advisoryLock is null)
        {
            DisposeLocks(fileLocks);
            throw new InvalidOperationException("Media cache advisory lock is unavailable.");
        }

        DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
        try
        {
            List<ApplyWork> ordered = BuildWork(manifest, config.MediaRoots);
            int batchCount = (ordered.Count + batchSize - 1) / batchSize;
            VideoVariantLooseCleanupJournal journal = LoadOrCreateJournal(
                manifest,
                fullJournalPath,
                batchSize,
                batchCount,
                resume,
                ordered
            );
            startedUtc = journal.StartedUtc;

            for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                VideoVariantLooseCleanupJournalBatch state = journal.Batches[batchIndex];
                IReadOnlyList<ApplyWork> batch = ordered
                    .Skip(batchIndex * batchSize)
                    .Take(batchSize)
                    .ToArray();
                if (state.State == "verified")
                    continue;
                bool resumedBatch = state.State != "pending";
                token.ThrowIfCancellationRequested();
                if (state.State is "pending" or "moving")
                {
                    state.State = "moving";
                    WriteJsonAtomic(fullJournalPath, journal);

                    CheckKeepFiles(batch, physicalParallelism, token);
                    if (physicalParallelism == 1)
                        foreach (ApplyWork work in batch)
                        {
                            token.ThrowIfCancellationRequested();
                            MoveWork(work, resumedBatch);
                        }
                    else
                        Parallel.ForEach(
                            batch,
                            new ParallelOptions
                            {
                                MaxDegreeOfParallelism = physicalParallelism,
                                CancellationToken = token,
                            },
                            work => MoveWork(work, resumedBatch)
                        );
                    state.State = "quarantined";
                    state.OriginalsQuarantined = batch.Count;
                    state.DirectQuarantined = batch.Count(value => value.Operation.DirectPresent);
                    state.BytesQuarantined = batch.Sum(value =>
                        value.Operation.OriginalSize
                        + (value.Operation.DirectPresent ? value.Operation.DirectSize : 0)
                    );
                    WriteJsonAtomic(fullJournalPath, journal);
                    FaultInjection?.Invoke("after-quarantined-journal");
                }

                if (state.State == "quarantined")
                {
                    VideoVariantLooseCleanupDeleteResult deleted = await database.DeleteBatchAsync(
                        manifest.PrimaryNamespace,
                        manifest.IncrementalNamespaces,
                        batch.Select(value => value.LogicalPath).ToArray(),
                        token
                    );
                    if (deleted.PrimaryDeleted < 0 || deleted.PrimaryDeleted > batch.Count)
                        throw new InvalidOperationException(
                            "Primary delete count is outside batch bounds."
                        );
                    if (!resume && deleted.PrimaryDeleted != batch.Count)
                        throw new InvalidOperationException(
                            $"Primary delete count mismatch: expected {batch.Count}, got {deleted.PrimaryDeleted}."
                        );
                    state.PrimaryDeleted += deleted.PrimaryDeleted;
                    state.IncrementalDeleted += deleted.IncrementalDeleted;
                    state.State = "db-committed";
                    WriteJsonAtomic(fullJournalPath, journal);
                    FaultInjection?.Invoke("after-db-commit");
                }

                if (state.State == "db-committed")
                {
                    VerifyBatchFiles(batch, physicalParallelism, token);
                    if (
                        !await database.AreRowsAbsentAsync(
                            manifest.PrimaryNamespace,
                            manifest.IncrementalNamespaces,
                            batch.Select(value => value.LogicalPath).ToArray(),
                            token
                        )
                    )
                        throw new InvalidOperationException(
                            "Target cache rows remain during batch verification."
                        );
                    state.State = "verified";
                    WriteJsonAtomic(fullJournalPath, journal);
                }
                progress?.Report(new(batchIndex + 1, batchCount, batch.Count, ordered.Count));
            }

            VideoVariantLooseCleanupReceipt receipt = new()
            {
                Version = 1,
                ApprovalHash = manifest.ApprovalHash,
                OperationSetHash = manifest.OperationSetHash,
                Host = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                RunId = journal.RunId,
                StartedUtc = startedUtc,
                CompletedUtc = DateTimeOffset.UtcNow,
                Operations = ordered.Count,
                OriginalsQuarantined = journal.Batches.Sum(value => value.OriginalsQuarantined),
                DirectQuarantined = journal.Batches.Sum(value => value.DirectQuarantined),
                BytesQuarantined = journal.Batches.Sum(value => value.BytesQuarantined),
                PrimaryDeleted = journal.Batches.Sum(value => value.PrimaryDeleted),
                IncrementalDeleted = journal.Batches.Sum(value => value.IncrementalDeleted),
                PrimaryNamespace = manifest.PrimaryNamespace,
                IncrementalNamespaces = [.. manifest.IncrementalNamespaces],
                QuarantineRoots = ordered
                    .SelectMany(value =>
                        value.Operation.DirectPresent
                            ? new[]
                            {
                                Path.GetDirectoryName(value.OriginalQuarantinePath)!,
                                Path.GetDirectoryName(value.DirectQuarantinePath)!,
                            }
                            : [Path.GetDirectoryName(value.OriginalQuarantinePath)!]
                    )
                    .Select(value => FindQuarantineRoot(value, manifest.ApprovalHash))
                    .Distinct(PhysicalPathSemantics.Comparer)
                    .Order(PhysicalPathSemantics.Comparer)
                    .ToList(),
                Batches = batchCount,
            };
            receipt.ReceiptHash = CalculateReceiptHash(receipt);
            WriteJsonAtomic(fullReceiptPath, receipt);
            File.Delete(fullJournalPath);
            return receipt;
        }
        finally
        {
            DisposeLocks(fileLocks);
        }
    }

    public async Task<VideoVariantLooseCleanupReceipt> ReconcileLegacyJournalAsync(
        VideoVariantLooseCleanupApplyManifest manifest,
        string sourceJournalPath,
        string approval,
        string journalPath,
        string receiptPath,
        bool resume,
        bool runtimeStoppedConfirmed,
        int physicalParallelism,
        IProgress<VideoVariantLooseCleanupApplyProgress>? progress = null,
        CancellationToken token = default
    )
    {
        if (!runtimeStoppedConfirmed)
            throw new InvalidOperationException("Runtime stopped confirmation is required.");
        if (physicalParallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(physicalParallelism));
        ValidateApproval(manifest, approval);
        ResolvedConfig config = ValidateCurrentConfig(manifest);
        List<ApplyWork> allWork = BuildWork(manifest, config.MediaRoots);
        byte[] sourceBytes = await File.ReadAllBytesAsync(sourceJournalPath, token);
        VideoVariantLooseCleanupJournal source = LoadAndValidateSourceJournal(
            manifest,
            sourceBytes,
            allWork
        );
        string sourceSha256 = Sha256(sourceBytes);
        VideoVariantLooseCleanupJournalBatch[] eligibleBatches = source
            .Batches.Where(batch => batch.State != "pending")
            .ToArray();
        if (eligibleBatches.Length == 0)
            throw new InvalidOperationException("Source apply journal has no non-pending batches.");
        List<int> workIndices = eligibleBatches
            .SelectMany(batch =>
                Enumerable.Range(
                    batch.Index * source.BatchSize,
                    Math.Min(source.BatchSize, allWork.Count - batch.Index * source.BatchSize)
                )
            )
            .Order()
            .ToList();
        ApplyWork[] eligibleWork = workIndices.Select(index => allWork[index]).ToArray();
        Dictionary<string, int> manifestIndices = manifest
            .Operations.Select((operation, index) => (operation, index))
            .ToDictionary(
                value => NormalizePath(manifest.Paths[value.operation.RemovePathIndex]),
                value => value.index,
                StringComparer.Ordinal
            );
        List<int> operationIndices = eligibleWork
            .Select(work => manifestIndices[NormalizePath(work.LogicalPath)])
            .Order()
            .ToList();
        string fullJournalPath = RequireLocalOutputPath(journalPath, nameof(journalPath));
        string fullReceiptPath = RequireLocalOutputPath(receiptPath, nameof(receiptPath));
        IVideoVariantLooseCleanupDatabase database = Database(config.ConnectionString);
        ValidateNamespaces(
            manifest,
            await database.DiscoverNamespacesAsync(
                StoreNamespace(manifest.MediaStoreId, manifest.CacheId),
                token
            )
        );
        if (!(await database.CheckIndexesAsync(token)).IsValid)
            throw new InvalidOperationException(
                "Required normalized path indexes are missing or invalid."
            );

        List<FileStream> fileLocks = AcquireFileLocks(
            config.MediaRoots.Values.Append(manifest.DirectRoot)
        );
        try
        {
            await using IAsyncDisposable? advisoryLock = await database.TryAcquireLockAsync(
                StoreNamespace(manifest.MediaStoreId, manifest.CacheId),
                token
            );
            if (advisoryLock is null)
                throw new InvalidOperationException("Media cache advisory lock is unavailable.");
            ValidateNamespaces(
                manifest,
                await database.DiscoverNamespacesAsync(
                    StoreNamespace(manifest.MediaStoreId, manifest.CacheId),
                    token
                )
            );
            if (File.Exists(fullReceiptPath))
            {
                VideoVariantLooseCleanupReceipt completed = LoadReceipt(fullReceiptPath);
                ValidateReconciliationReceipt(manifest, completed, sourceSha256, operationIndices);
                return completed;
            }

            VideoVariantLooseCleanupReconciliationJournal journal =
                LoadOrCreateReconciliationJournal(
                    manifest,
                    source,
                    sourceSha256,
                    eligibleBatches,
                    fullJournalPath,
                    resume
                );

            for (int index = 0; index < journal.Batches.Count; index++)
            {
                VideoVariantLooseCleanupReconciliationBatch state = journal.Batches[index];
                ApplyWork[] batch = allWork
                    .Skip(state.StartOperationIndex)
                    .Take(state.OperationCount)
                    .ToArray();
                token.ThrowIfCancellationRequested();
                if (state.State == "pending")
                {
                    if (state.SourceState == "moving")
                    {
                        CheckKeepFiles(batch, physicalParallelism, token);
                        RunPhysical(
                            batch,
                            physicalParallelism,
                            token,
                            work => MoveWork(work, true)
                        );
                        FaultInjection?.Invoke("after-reconcile-moves");
                    }
                    VerifyBatchFiles(batch, physicalParallelism, token);
                    state.State = "quarantined";
                    WriteReconciliationJournal(fullJournalPath, journal);
                }
                if (state.State == "quarantined")
                {
                    VerifyBatchFiles(batch, physicalParallelism, token);
                    IReadOnlyDictionary<string, int> counts = await LoadRowCounts(
                        database,
                        manifest,
                        batch,
                        token
                    );
                    bool committed = state.SourceState is "db-committed" or "verified";
                    if (counts.Values.All(count => count == 1) && !committed)
                    {
                        VideoVariantLooseCleanupDeleteResult deleted =
                            await database.DeleteBatchAsync(
                                manifest.PrimaryNamespace,
                                manifest.IncrementalNamespaces,
                                batch.Select(work => NormalizePath(work.LogicalPath)).ToArray(),
                                token
                            );
                        if (deleted.PrimaryDeleted + deleted.IncrementalDeleted != batch.Length)
                            throw new InvalidOperationException(
                                "Reconciliation database delete count mismatch."
                            );
                        state.PrimaryDeleted = deleted.PrimaryDeleted;
                        state.IncrementalDeleted = deleted.IncrementalDeleted;
                    }
                    else if (!counts.Values.All(count => count == 0))
                        throw new InvalidOperationException(
                            committed
                                ? "Committed source batch still has cache rows."
                                : "Reconciliation database state is partial or duplicated."
                        );
                    if (
                        !await database.AreRowsAbsentAsync(
                            manifest.PrimaryNamespace,
                            manifest.IncrementalNamespaces,
                            batch.Select(work => NormalizePath(work.LogicalPath)).ToArray(),
                            token
                        )
                    )
                        throw new InvalidOperationException(
                            "Target cache rows remain after reconciliation."
                        );
                    state.State = "db-committed";
                    WriteReconciliationJournal(fullJournalPath, journal);
                    FaultInjection?.Invoke("after-reconcile-db-commit");
                }
                if (state.State == "db-committed")
                {
                    VerifyBatchFiles(batch, physicalParallelism, token);
                    await RequireExactRowCounts(database, manifest, batch, 0, token);
                    state.State = "verified";
                    WriteReconciliationJournal(fullJournalPath, journal);
                }
                progress?.Report(
                    new(index + 1, journal.Batches.Count, batch.Length, eligibleWork.Length)
                );
            }

            VideoVariantLooseCleanupReceipt receipt = new()
            {
                Version = 1,
                ApprovalHash = manifest.ApprovalHash,
                OperationSetHash = manifest.OperationSetHash,
                SourceJournalSha256 = sourceSha256,
                OperationIndices = operationIndices,
                Host = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                RunId = journal.RunId,
                StartedUtc = journal.StartedUtc,
                CompletedUtc = DateTimeOffset.UtcNow,
                Operations = eligibleWork.Length,
                OriginalsQuarantined = eligibleWork.Length,
                DirectQuarantined = eligibleWork.Count(work => work.Operation.DirectPresent),
                BytesQuarantined = eligibleWork.Sum(work =>
                    work.Operation.OriginalSize
                    + (work.Operation.DirectPresent ? work.Operation.DirectSize : 0)
                ),
                PrimaryDeleted = journal.Batches.Sum(batch =>
                    batch.SourcePrimaryDeleted + batch.PrimaryDeleted
                ),
                IncrementalDeleted = journal.Batches.Sum(batch =>
                    batch.SourceIncrementalDeleted + batch.IncrementalDeleted
                ),
                PrimaryNamespace = manifest.PrimaryNamespace,
                IncrementalNamespaces = [.. manifest.IncrementalNamespaces],
                QuarantineRoots = eligibleWork
                    .SelectMany(work =>
                        work.Operation.DirectPresent
                            ? new[]
                            {
                                FindQuarantineRoot(
                                    work.OriginalQuarantinePath,
                                    manifest.ApprovalHash
                                ),
                                FindQuarantineRoot(
                                    work.DirectQuarantinePath,
                                    manifest.ApprovalHash
                                ),
                            }
                            :
                            [
                                FindQuarantineRoot(
                                    work.OriginalQuarantinePath,
                                    manifest.ApprovalHash
                                ),
                            ]
                    )
                    .Distinct(PhysicalPathSemantics.Comparer)
                    .Order(PhysicalPathSemantics.Comparer)
                    .ToList(),
                Batches = journal.Batches.Count,
            };
            if (Sha256(await File.ReadAllBytesAsync(sourceJournalPath, token)) != sourceSha256)
                throw new InvalidOperationException("Frozen source apply journal changed.");
            receipt.ReceiptHash = CalculateReceiptHash(receipt);
            WriteJsonAtomic(fullReceiptPath, receipt);
            File.Delete(fullJournalPath);
            return receipt;
        }
        finally
        {
            DisposeLocks(fileLocks);
        }
    }

    public async Task VerifyAsync(
        VideoVariantLooseCleanupApplyManifest manifest,
        string receiptPath,
        IProgress<VideoVariantLooseCleanupApplyProgress>? progress = null,
        CancellationToken token = default
    )
    {
        ResolvedConfig config = ValidateCurrentConfig(manifest);
        VideoVariantLooseCleanupReceipt receipt = LoadReceipt(receiptPath);
        if (
            receipt.ApprovalHash != manifest.ApprovalHash
            || receipt.OperationSetHash != manifest.OperationSetHash
        )
            throw new InvalidOperationException("Receipt does not match manifest.");
        List<ApplyWork> allWork = BuildWork(manifest, config.MediaRoots);
        IReadOnlyList<int> operationIndices = ValidateReceiptOperationIndices(
            receipt,
            allWork.Count
        );
        ApplyWork[] work = operationIndices.Select(index => allWork[index]).ToArray();
        ApplyWork[] directWork = work.Where(value => value.Operation.DirectPresent).ToArray();
        int total = work.Length + directWork.Length;
        int completed = 0;
        List<ApplyWork[]> driveWork = work.GroupBy(value => value.Operation.PartitionId)
            .Select(group => group.ToArray())
            .Append(directWork)
            .Where(group => group.Length > 0)
            .ToList();
        await Parallel.ForEachAsync(
            driveWork,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = driveWork.Count,
                CancellationToken = token,
            },
            (group, _) =>
            {
                bool direct = ReferenceEquals(group, directWork);

                foreach (ApplyWork item in group)
                {
                    token.ThrowIfCancellationRequested();
                    if (direct)
                        VerifyDirectFiles(item);
                    else
                        VerifyOriginalFiles(item);
                    int current = Interlocked.Increment(ref completed);
                    if (current % 1000 == 0 || current == total)
                        progress?.Report(new(current, total, current, total));
                }
                return ValueTask.CompletedTask;
            }
        );
        IVideoVariantLooseCleanupDatabase database = Database(config.ConnectionString);
        if (
            !await database.AreRowsAbsentAsync(
                manifest.PrimaryNamespace,
                manifest.IncrementalNamespaces,
                work.Select(value => value.LogicalPath).ToArray(),
                token
            )
        )
            throw new InvalidOperationException("Target cache rows remain.");
    }

    internal static IReadOnlyList<int> ValidateReceiptOperationIndices(
        VideoVariantLooseCleanupReceipt receipt,
        int operationCount
    )
    {
        IReadOnlyList<int> indices =
            receipt.OperationIndices
            ?? (IReadOnlyList<int>)Enumerable.Range(0, operationCount).ToArray();
        if (
            indices.Count != receipt.Operations
            || indices.Any(index => index < 0 || index >= operationCount)
            || !indices.SequenceEqual(indices.Order())
            || indices.Distinct().Count() != indices.Count
        )
            throw new InvalidOperationException("Apply receipt operation indices are invalid.");
        return indices;
    }

    internal static string CalculateOperationSetHash(VideoVariantLooseCleanupApplyManifest manifest)
    {
        var canonical = manifest
            .Operations.Select(operation => new
            {
                Remove = manifest.Paths[operation.RemovePathIndex],
                Keep = manifest.Paths[operation.KeepPathIndex],
                operation.PartitionId,
                operation.KeepPartitionId,
                operation.OriginalSize,
                operation.OriginalLastWriteUtcTicks,
                operation.DirectPresent,
                operation.DirectSize,
                operation.DirectLastWriteUtcTicks,
            })
            .OrderBy(value => value.PartitionId)
            .ThenBy(value => value.Remove, StringComparer.Ordinal)
            .ToArray();
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(canonical));
    }

    internal static string CalculateApprovalHash(VideoVariantLooseCleanupApplyManifest manifest)
    {
        VideoVariantLooseCleanupApplyManifest canonical = manifest with
        {
            ApprovalHash = string.Empty,
        };
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(canonical, JsonOptions));
    }

    internal static string CalculateReceiptHash(VideoVariantLooseCleanupReceipt receipt)
    {
        VideoVariantLooseCleanupReceipt canonical = receipt with { ReceiptHash = string.Empty };
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(canonical, JsonOptions));
    }

    private static void ValidateSource(VideoVariantLooseCleanupManifest source)
    {
        if (source.Version != 1 || !source.PhysicalChecked)
            throw new InvalidOperationException("Source manifest must be physical version 1.");
        if (source.SelectionLimit is not null)
            throw new InvalidOperationException("Source manifest selection limit must be null.");
        ValidateSourceIssues(source);
        if (
            VideoVariantLooseCleanupPreviewService.CalculateApprovalHash(source)
            != source.ApprovalHash
        )
            throw new InvalidOperationException("Source manifest approval hash is invalid.");
        ValidateOperationRelationships(
            source.Paths,
            source.Operations.Select(value => (value.RemovePathIndex, value.KeepPathIndex))
        );
    }

    private static void ValidateSourceIssues(VideoVariantLooseCleanupManifest source)
    {
        if (source.Issues.Count == 0)
            return;

        HashSet<string> operationGroups = source
            .Operations.SelectMany(operation =>
                new[]
                {
                    source.Paths[operation.RemovePathIndex],
                    source.Paths[operation.KeepPathIndex],
                }
            )
            .Where(IsThumbPath)
            .Select(GetThumbGroup)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> issueGroups = new(StringComparer.OrdinalIgnoreCase);

        foreach (VideoVariantLooseCleanupIssue issue in source.Issues)
        {
            if (issue.Code != "keep-set-invalid")
                throw new InvalidOperationException("Source manifest contains a fatal issue.");
            string group = GetThumbGroup(issue.Path);
            if (!issueGroups.Add(group))
                throw new InvalidOperationException(
                    "Source manifest contains duplicate issue groups."
                );
            if (operationGroups.Contains(group))
                throw new InvalidOperationException(
                    "Source manifest operates on an invalid thumbnail group."
                );
        }
    }

    private static string GetThumbGroup(string path)
    {
        string[] parts = NormalizePath(path).Split('/');
        if (parts.Length < 5 || !(parts[1] is "gif" or "video") || parts[3] != "thumb")
            throw new InvalidOperationException("Issue path is not a thumbnail path.");
        return string.Join('/', parts.Take(5));
    }

    private static bool IsThumbPath(string path)
    {
        string[] parts = NormalizePath(path).Split('/');
        return parts.Length >= 5 && parts[1] is "gif" or "video" && parts[3] == "thumb";
    }

    private static void ValidateManifestStructure(
        VideoVariantLooseCleanupApplyManifest manifest,
        bool verifyHash
    )
    {
        if (manifest.Version != 3)
            throw new InvalidOperationException("Loose cleanup apply manifest version must be 3.");
        if (manifest.Paths.Count != manifest.Paths.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidOperationException("Manifest path table contains duplicates.");
        if (
            !manifest.IncrementalNamespaces.SequenceEqual(
                manifest
                    .IncrementalNamespaces.Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
                StringComparer.Ordinal
            )
        )
            throw new InvalidOperationException(
                "Incremental namespaces must be unique and ordered."
            );
        ValidateOperationRelationships(
            manifest.Paths,
            manifest.Operations.Select(value => (value.RemovePathIndex, value.KeepPathIndex))
        );
        if (CalculateOperationSetHash(manifest) != manifest.OperationSetHash)
            throw new InvalidOperationException("Operation set hash is invalid.");
        if (verifyHash && CalculateApprovalHash(manifest) != manifest.ApprovalHash)
            throw new InvalidOperationException("Apply manifest approval hash is invalid.");
    }

    private static void ValidateManifest(
        VideoVariantLooseCleanupApplyManifest manifest,
        IReadOnlyDictionary<int, string> roots,
        bool verifyHash
    )
    {
        ValidateManifestStructure(manifest, verifyHash);

        foreach (VideoVariantLooseCleanupApplyOperation operation in manifest.Operations)
        {
            if (!roots.TryGetValue(operation.PartitionId, out string? root))
                throw new InvalidOperationException(
                    $"Partition {operation.PartitionId} is unresolved."
                );
            if (!roots.TryGetValue(operation.KeepPartitionId, out string? keepRoot))
                throw new InvalidOperationException(
                    $"Keep partition {operation.KeepPartitionId} is unresolved."
                );
            string remove = manifest.Paths[operation.RemovePathIndex];
            _ = VideoVariantLooseCleanupPreviewService.ResolveContainedPath(root, remove);
            _ = VideoVariantLooseCleanupPreviewService.ResolveContainedPath(
                keepRoot,
                manifest.Paths[operation.KeepPathIndex]
            );
            _ = VideoVariantLooseCleanupPreviewService.ResolveContainedPath(
                manifest.DirectRoot,
                remove
            );
            if (
                operation.OriginalSize < 0
                || operation.OriginalLastWriteUtcTicks <= 0
                || operation.DirectSize < 0
                || (operation.DirectPresent && operation.DirectLastWriteUtcTicks <= 0)
            )
                throw new InvalidOperationException("Operation physical metadata is invalid.");
        }
    }

    private static void ValidateOperationRelationships(
        IReadOnlyList<string> paths,
        IEnumerable<(int Remove, int Keep)> operations
    )
    {
        List<(int Remove, int Keep)> values = operations.ToList();

        foreach ((int remove, int keep) in values)
            if (remove < 0 || remove >= paths.Count || keep < 0 || keep >= paths.Count)
                throw new InvalidOperationException("Manifest path index is out of range.");
        HashSet<string> removes = [];

        foreach ((int remove, int keep) in values)
        {
            string removePath = NormalizePath(paths[remove]);
            string keepPath = NormalizePath(paths[keep]);
            if (removePath == keepPath)
                throw new InvalidOperationException("Remove path equals keep path.");
            if (!removes.Add(removePath))
                throw new InvalidOperationException("Duplicate remove path.");
        }
        if (values.Any(value => removes.Contains(NormalizePath(paths[value.Keep]))))
            throw new InvalidOperationException("Keep path is also removed.");
    }

    private ResolvedConfig ValidateCurrentConfig(VideoVariantLooseCleanupApplyManifest manifest)
    {
        ValidateManifestStructure(manifest, verifyHash: true);
        FileInfo info = new(manifest.DataConfigPath);
        if (
            !info.Exists
            || info.Length != manifest.DataConfigLength
            || info.LastWriteTimeUtc.Ticks != manifest.DataConfigLastWriteUtcTicks
        )
            throw new InvalidOperationException("Data config fingerprint changed.");
        if (Sha256(File.ReadAllBytes(info.FullName)) != manifest.DataConfigSha256)
            throw new InvalidOperationException("Data config SHA256 changed.");
        ResolvedConfig config = VideoVariantLooseCleanupPreviewService.LoadConfig(
            manifest.DataConfigPath,
            manifest.MediaStoreId,
            manifest.CacheId
        );
        ValidateManifest(manifest, config.MediaRoots, verifyHash: true);
        return config;
    }

    private static void ValidateApproval(
        VideoVariantLooseCleanupApplyManifest manifest,
        string approval
    )
    {
        ValidateManifestStructure(manifest, verifyHash: true);
        if (!approval.Equals(manifest.ApprovalHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Approval hash does not match manifest.");
    }

    private static void ValidateNamespaces(
        VideoVariantLooseCleanupApplyManifest manifest,
        VideoVariantLooseCleanupNamespaces current
    )
    {
        if (
            current.PrimaryNamespace != manifest.PrimaryNamespace
            || !current
                .IncrementalNamespaces.Order(StringComparer.Ordinal)
                .SequenceEqual(manifest.IncrementalNamespaces, StringComparer.Ordinal)
        )
            throw new InvalidOperationException("Cache namespaces changed.");
    }

    private static List<ApplyWork> BuildWork(
        VideoVariantLooseCleanupApplyManifest manifest,
        IReadOnlyDictionary<int, string> roots
    ) =>
        manifest
            .Operations.Select(operation =>
            {
                string logical = manifest.Paths[operation.RemovePathIndex];
                string hash = Sha256(
                    Encoding.UTF8.GetBytes($"original\n{operation.PartitionId}\n{logical}")
                );
                string directHash = Sha256(
                    Encoding.UTF8.GetBytes($"direct\n{operation.PartitionId}\n{logical}")
                );
                string originalRoot = roots[operation.PartitionId];
                string original = VideoVariantLooseCleanupPreviewService.ResolveContainedPath(
                    originalRoot,
                    logical
                );
                string direct = VideoVariantLooseCleanupPreviewService.ResolveContainedPath(
                    manifest.DirectRoot,
                    logical
                );
                string keepLogical = manifest.Paths[operation.KeepPathIndex];
                string keep = VideoVariantLooseCleanupPreviewService.ResolveContainedPath(
                    roots[operation.KeepPartitionId],
                    keepLogical
                );
                string originalQuarantine = Path.Combine(
                    originalRoot,
                    QuarantineDirectory,
                    manifest.ApprovalHash,
                    "original",
                    operation.PartitionId.ToString(),
                    hash[..2],
                    hash + ".bin"
                );
                string directQuarantine = Path.Combine(
                    manifest.DirectRoot,
                    QuarantineDirectory,
                    manifest.ApprovalHash,
                    "direct",
                    directHash[..2],
                    directHash + ".bin"
                );
                RequireSameVolume(original, originalQuarantine);
                RequireSameVolume(direct, directQuarantine);
                return new ApplyWork(
                    operation,
                    logical,
                    keep,
                    original,
                    direct,
                    originalQuarantine,
                    directQuarantine
                );
            })
            .OrderBy(value => value.Operation.PartitionId)
            .ThenBy(value => value.LogicalPath, StringComparer.Ordinal)
            .ToList();

    private static VideoVariantLooseCleanupJournal LoadOrCreateJournal(
        VideoVariantLooseCleanupApplyManifest manifest,
        string path,
        int batchSize,
        int batchCount,
        bool resume,
        IReadOnlyCollection<ApplyWork> work
    )
    {
        if (File.Exists(path))
        {
            if (!resume)
                throw new InvalidOperationException("Existing journal requires --resume.");
            VideoVariantLooseCleanupJournal existing =
                JsonSerializer.Deserialize<VideoVariantLooseCleanupJournal>(
                    File.ReadAllBytes(path),
                    JsonOptions
                ) ?? throw new InvalidOperationException("Invalid apply journal.");
            if (
                existing.ApprovalHash != manifest.ApprovalHash
                || existing.OperationSetHash != manifest.OperationSetHash
                || existing.BatchSize != batchSize
                || existing.Batches.Count != batchCount
            )
                throw new InvalidOperationException(
                    "Journal does not match manifest or batch size."
                );
            return existing;
        }
        if (resume)
            throw new InvalidOperationException("Resume requested but journal does not exist.");
        string[] quarantineRoots = work.SelectMany(value =>
                value.Operation.DirectPresent
                    ? new[]
                    {
                        FindQuarantineRoot(value.OriginalQuarantinePath, manifest.ApprovalHash),
                        FindQuarantineRoot(value.DirectQuarantinePath, manifest.ApprovalHash),
                    }
                    : [FindQuarantineRoot(value.OriginalQuarantinePath, manifest.ApprovalHash)]
            )
            .Distinct(PhysicalPathSemantics.Comparer)
            .ToArray();
        if (quarantineRoots.Any(Directory.Exists))
            throw new InvalidOperationException("Quarantine root exists without journal.");
        VideoVariantLooseCleanupJournal journal = new()
        {
            ApprovalHash = manifest.ApprovalHash,
            OperationSetHash = manifest.OperationSetHash,
            RunId = Guid.NewGuid(),
            StartedUtc = DateTimeOffset.UtcNow,
            BatchSize = batchSize,
            Batches = Enumerable
                .Range(0, batchCount)
                .Select(index => new VideoVariantLooseCleanupJournalBatch { Index = index })
                .ToList(),
        };
        WriteJsonAtomic(path, journal);
        return journal;
    }

    private void Move(
        string source,
        string destination,
        long size,
        long ticks,
        string label,
        bool inferState
    )
    {
        if (inferState)
        {
            bool sourceExists = File.Exists(source);
            bool destinationExists = File.Exists(destination);
            if (sourceExists == destinationExists)
                throw new InvalidOperationException($"{label} move state is ambiguous: {source}");
            if (destinationExists)
            {
                RequireFile(destination, size, ticks, $"Quarantined {label.ToLowerInvariant()}");
                return;
            }
        }
        RequireFile(source, size, ticks, label);
        EnsureDirectory(Path.GetDirectoryName(destination)!);

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(source, destination);
                return;
            }
            catch (IOException) when (attempt < 3)
            {
                if (!File.Exists(source) && File.Exists(destination))
                {
                    RequireFile(
                        destination,
                        size,
                        ticks,
                        $"Quarantined {label.ToLowerInvariant()}"
                    );
                    return;
                }
                Thread.Sleep(100 * (attempt + 1));
            }
        }
    }

    private void EnsureDirectory(string path)
    {
        Lazy<bool> created = _createdDirectories.GetOrAdd(
            path,
            value => new Lazy<bool>(
                () =>
                {
                    Directory.CreateDirectory(value);
                    return true;
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );
        try
        {
            _ = created.Value;
        }
        catch
        {
            _createdDirectories.TryRemove(new KeyValuePair<string, Lazy<bool>>(path, created));
            throw;
        }
    }

    private static void RequireFile(string path, long? size, long? ticks, string label)
    {
        FileInfo info = new(path);
        if (!info.Exists)
            throw new InvalidOperationException($"{label} is absent: {path}");
        if (size.HasValue && info.Length != size.Value)
            throw new InvalidOperationException($"{label} size changed: {path}");
        if (ticks.HasValue && info.LastWriteTimeUtc.Ticks != ticks.Value)
            throw new InvalidOperationException($"{label} timestamp changed: {path}");
    }

    private static void CheckKeepFiles(
        IEnumerable<ApplyWork> batch,
        int physicalParallelism,
        CancellationToken token
    )
    {
        string[] paths = batch
            .Select(work => work.KeepPath)
            .Distinct(PhysicalPathSemantics.Comparer)
            .ToArray();
        if (physicalParallelism == 1)
        {
            foreach (string path in paths)
            {
                token.ThrowIfCancellationRequested();
                RequireFile(path, null, null, "Keep file");
            }
            return;
        }
        Parallel.ForEach(
            paths,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = physicalParallelism,
                CancellationToken = token,
            },
            path => RequireFile(path, null, null, "Keep file")
        );
    }

    private void MoveWork(ApplyWork work, bool inferState)
    {
        if (work.Operation.DirectPresent)
        {
            Move(
                work.DirectPath,
                work.DirectQuarantinePath,
                work.Operation.DirectSize,
                work.Operation.DirectLastWriteUtcTicks,
                "Direct file",
                inferState
            );
            InvokeFault("after-direct-move");
        }
        Move(
            work.OriginalPath,
            work.OriginalQuarantinePath,
            work.Operation.OriginalSize,
            work.Operation.OriginalLastWriteUtcTicks,
            "Original file",
            inferState
        );
        InvokeFault("after-original-move");
    }

    private void InvokeFault(string stage)
    {
        Action<string>? fault = FaultInjection;
        if (fault is null)
            return;

        lock (fault)
            fault(stage);
    }

    private static void VerifyBatchFiles(
        IEnumerable<ApplyWork> batch,
        int physicalParallelism,
        CancellationToken token
    )
    {
        if (physicalParallelism == 1)
        {
            foreach (ApplyWork work in batch)
            {
                token.ThrowIfCancellationRequested();
                VerifyWorkFiles(work);
            }
            return;
        }
        Parallel.ForEach(
            batch,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = physicalParallelism,
                CancellationToken = token,
            },
            VerifyWorkFiles
        );
    }

    private static void VerifySameRunDirectFiles(
        IEnumerable<ApplyWork> batch,
        int physicalParallelism,
        CancellationToken token
    )
    {
        ApplyWork[] eligible = batch
            .Where(work =>
                work.Operation.DirectPresent
                || work.Operation.OriginalSize
                    > VideoVariantLooseCleanupPreviewService.DirectMinimumOriginalSize
            )
            .ToArray();
        if (eligible.Length == 0)
            return;

        Parallel.ForEach(
            eligible,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = physicalParallelism,
                CancellationToken = token,
            },
            work =>
            {
                if (work.Operation.DirectPresent)
                    VerifyDirectFiles(work);
                else if (File.Exists(work.DirectPath) || File.Exists(work.DirectQuarantinePath))
                    throw new InvalidOperationException(
                        $"Unexpected direct file: {work.LogicalPath}"
                    );
            }
        );
    }

    private static void VerifyWorkFiles(ApplyWork work)
    {
        VerifyOriginalFiles(work);
        if (work.Operation.DirectPresent)
            VerifyDirectFiles(work);
        else if (File.Exists(work.DirectPath) || File.Exists(work.DirectQuarantinePath))
            throw new InvalidOperationException($"Unexpected direct file: {work.LogicalPath}");
    }

    private static void VerifyOriginalFiles(ApplyWork work)
    {
        if (File.Exists(work.OriginalPath))
            throw new InvalidOperationException($"Active remove path remains: {work.LogicalPath}");
        RequireFile(
            work.OriginalQuarantinePath,
            work.Operation.OriginalSize,
            work.Operation.OriginalLastWriteUtcTicks,
            "Quarantined original"
        );
    }

    private static void VerifyDirectFiles(ApplyWork work)
    {
        if (File.Exists(work.DirectPath))
            throw new InvalidOperationException(
                $"Active direct remove path remains: {work.LogicalPath}"
            );
        RequireFile(
            work.DirectQuarantinePath,
            work.Operation.DirectSize,
            work.Operation.DirectLastWriteUtcTicks,
            "Quarantined direct"
        );
    }

    private static List<FileStream> AcquireFileLocks(IEnumerable<string> roots)
    {
        List<FileStream> locks = [];
        try
        {
            foreach (
                string root in roots
                    .Select(Path.GetFullPath)
                    .Distinct(PhysicalPathSemantics.Comparer)
            )
            {
                if (!Directory.Exists(root))
                    throw new DirectoryNotFoundException($"Media root does not exist: {root}");
                locks.Add(
                    new FileStream(
                        Path.Combine(root, LockFileName),
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None
                    )
                );
            }
            return locks;
        }
        catch
        {
            DisposeLocks(locks);
            throw;
        }
    }

    private static void DisposeLocks(IEnumerable<FileStream> locks)
    {
        foreach (FileStream stream in locks)
            stream.Dispose();
    }

    private VideoVariantLooseCleanupReceipt LoadReceipt(string path)
    {
        VideoVariantLooseCleanupReceipt receipt =
            JsonSerializer.Deserialize<VideoVariantLooseCleanupReceipt>(
                File.ReadAllBytes(path),
                JsonOptions
            ) ?? throw new InvalidOperationException("Invalid loose cleanup receipt.");
        if (CalculateReceiptHash(receipt) != receipt.ReceiptHash)
            throw new InvalidOperationException("Receipt hash is invalid.");
        return receipt;
    }

    internal static string CalculateReconciliationJournalHash(
        VideoVariantLooseCleanupReconciliationJournal journal
    ) =>
        Sha256(
            JsonSerializer.SerializeToUtf8Bytes(
                journal with
                {
                    JournalHash = string.Empty,
                },
                JsonOptions
            )
        );

    private static VideoVariantLooseCleanupReconciliationJournal LoadOrCreateReconciliationJournal(
        VideoVariantLooseCleanupApplyManifest manifest,
        VideoVariantLooseCleanupJournal source,
        string sourceSha256,
        IReadOnlyList<VideoVariantLooseCleanupJournalBatch> eligible,
        string path,
        bool resume
    )
    {
        if (File.Exists(path))
        {
            if (!resume)
                throw new InvalidOperationException(
                    "Existing reconciliation journal requires --resume."
                );
            VideoVariantLooseCleanupReconciliationJournal existing =
                JsonSerializer.Deserialize<VideoVariantLooseCleanupReconciliationJournal>(
                    File.ReadAllBytes(path),
                    JsonOptions
                ) ?? throw new InvalidOperationException("Invalid reconciliation journal.");
            ValidateReconciliationJournal(existing, manifest, source, sourceSha256, eligible);
            return existing;
        }
        if (resume)
            throw new InvalidOperationException(
                "Resume requested but reconciliation journal does not exist."
            );
        VideoVariantLooseCleanupReconciliationJournal journal = new()
        {
            Version = 1,
            Mode = "reconcile-apply-loose-v1",
            ManifestApprovalHash = manifest.ApprovalHash,
            OperationSetHash = manifest.OperationSetHash,
            SourceJournalSha256 = sourceSha256,
            SourceJournalRunId = source.RunId,
            RunId = Guid.NewGuid(),
            StartedUtc = DateTimeOffset.UtcNow,
            Batches = eligible
                .Select(batch => new VideoVariantLooseCleanupReconciliationBatch
                {
                    SourceBatchIndex = batch.Index,
                    SourceState = batch.State,
                    SourceOriginalsQuarantined = batch.OriginalsQuarantined,
                    SourceDirectQuarantined = batch.DirectQuarantined,
                    SourceBytesQuarantined = batch.BytesQuarantined,
                    SourcePrimaryDeleted = batch.PrimaryDeleted,
                    SourceIncrementalDeleted = batch.IncrementalDeleted,
                    StartOperationIndex = batch.Index * source.BatchSize,
                    OperationCount = Math.Min(
                        source.BatchSize,
                        manifest.Operations.Count - batch.Index * source.BatchSize
                    ),
                })
                .ToList(),
        };
        WriteReconciliationJournal(path, journal);
        return journal;
    }

    private static void WriteReconciliationJournal(
        string path,
        VideoVariantLooseCleanupReconciliationJournal journal
    )
    {
        ValidateReconciliationJournalState(journal);
        journal.JournalHash = CalculateReconciliationJournalHash(journal);
        WriteJsonAtomic(path, journal);
    }

    private static void ValidateReconciliationJournal(
        VideoVariantLooseCleanupReconciliationJournal journal,
        VideoVariantLooseCleanupApplyManifest manifest,
        VideoVariantLooseCleanupJournal source,
        string sourceSha256,
        IReadOnlyList<VideoVariantLooseCleanupJournalBatch> eligible
    )
    {
        if (
            journal.Version != 1
            || journal.Mode != "reconcile-apply-loose-v1"
            || journal.ManifestApprovalHash != manifest.ApprovalHash
            || journal.OperationSetHash != manifest.OperationSetHash
            || journal.SourceJournalSha256 != sourceSha256
            || journal.SourceJournalRunId != source.RunId
            || journal.Batches.Count != eligible.Count
            || CalculateReconciliationJournalHash(journal) != journal.JournalHash
        )
            throw new InvalidOperationException("Reconciliation journal does not match source.");

        for (int index = 0; index < eligible.Count; index++)
        {
            VideoVariantLooseCleanupJournalBatch sourceBatch = eligible[index];
            VideoVariantLooseCleanupReconciliationBatch batch = journal.Batches[index];
            if (
                batch.SourceBatchIndex != sourceBatch.Index
                || batch.SourceState != sourceBatch.State
                || batch.SourceOriginalsQuarantined != sourceBatch.OriginalsQuarantined
                || batch.SourceDirectQuarantined != sourceBatch.DirectQuarantined
                || batch.SourceBytesQuarantined != sourceBatch.BytesQuarantined
                || batch.SourcePrimaryDeleted != sourceBatch.PrimaryDeleted
                || batch.SourceIncrementalDeleted != sourceBatch.IncrementalDeleted
                || batch.StartOperationIndex != sourceBatch.Index * source.BatchSize
                || batch.OperationCount
                    != Math.Min(
                        source.BatchSize,
                        manifest.Operations.Count - sourceBatch.Index * source.BatchSize
                    )
            )
                throw new InvalidOperationException(
                    "Reconciliation journal batch metadata changed."
                );
        }
        ValidateReconciliationJournalState(journal);
    }

    private static void ValidateReconciliationJournalState(
        VideoVariantLooseCleanupReconciliationJournal journal
    )
    {
        if (
            journal.Batches.Any(batch =>
                batch.SourceState is not ("moving" or "quarantined" or "db-committed" or "verified")
                || batch.State is not ("pending" or "quarantined" or "db-committed" or "verified")
                || batch.StartOperationIndex < 0
                || batch.OperationCount <= 0
                || batch.PrimaryDeleted < 0
                || batch.IncrementalDeleted < 0
                || batch.PrimaryDeleted + batch.IncrementalDeleted > batch.OperationCount
                || (
                    batch.State is "pending" or "quarantined"
                    && (batch.PrimaryDeleted != 0 || batch.IncrementalDeleted != 0)
                )
            )
        )
            throw new InvalidOperationException("Reconciliation journal state is invalid.");
    }

    private static void ValidateReconciliationReceipt(
        VideoVariantLooseCleanupApplyManifest manifest,
        VideoVariantLooseCleanupReceipt receipt,
        string sourceSha256,
        IReadOnlyList<int> operationIndices
    )
    {
        if (
            receipt.ApprovalHash != manifest.ApprovalHash
            || receipt.OperationSetHash != manifest.OperationSetHash
            || receipt.SourceJournalSha256 != sourceSha256
            || receipt.OperationIndices is null
            || !receipt.OperationIndices.SequenceEqual(operationIndices)
            || receipt.Operations != operationIndices.Count
        )
            throw new InvalidOperationException("Reconciliation receipt does not match source.");
    }

    internal static string CalculateDirectDeletePlanHash(
        VideoVariantLooseCleanupDirectDeletePlan plan
    ) =>
        Sha256(
            JsonSerializer.SerializeToUtf8Bytes(
                plan with
                {
                    ApprovalHash = string.Empty,
                },
                JsonOptions
            )
        );

    internal static string CalculateDirectDeleteReceiptHash(
        VideoVariantLooseCleanupDirectDeleteReceipt receipt
    ) =>
        Sha256(
            JsonSerializer.SerializeToUtf8Bytes(
                receipt with
                {
                    ReceiptHash = string.Empty,
                },
                JsonOptions
            )
        );

    internal static string CalculateDirectDeleteJournalHash(
        VideoVariantLooseCleanupDirectDeleteJournal journal
    ) =>
        Sha256(
            JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    journal.Version,
                    journal.Mode,
                    journal.PlanApprovalHash,
                    journal.RunId,
                    journal.StartedUtc,
                    journal.BatchSize,
                    journal.Batches,
                    JournalHash = string.Empty,
                },
                JsonOptions
            )
        );

    private static void ValidateDirectDeletePlanStructure(
        VideoVariantLooseCleanupDirectDeletePlan plan
    )
    {
        if (plan.Version != 1)
            throw new InvalidOperationException("Direct-delete plan version must be 1.");
        if (
            plan.Operations <= 0
            || plan.Operations > 10_000
            || plan.Operations != plan.OperationIndices.Count
            || plan.Files < plan.Operations
            || plan.Files > plan.Operations * 2
            || plan.Bytes < 0
            || plan.SourceJournalBatchSize <= 0
            || plan.SourceJournalBatchCount <= 0
            || plan.OperationIndices.Any(index => index < 0)
            || !plan.OperationIndices.SequenceEqual(plan.OperationIndices.Order())
            || plan.OperationIndices.Distinct().Count() != plan.OperationIndices.Count
            || plan.Keeps.Count == 0
            || plan.Keeps.Any(keep =>
                string.IsNullOrWhiteSpace(keep.Path) || keep.Size < 0 || keep.LastWriteUtcTicks <= 0
            )
            || plan.Keeps.Select(keep => keep.Path).Distinct(PhysicalPathSemantics.Comparer).Count()
                != plan.Keeps.Count
        )
            throw new InvalidOperationException("Direct-delete plan structure is invalid.");
        if (CalculateDirectDeletePlanHash(plan) != plan.ApprovalHash)
            throw new InvalidOperationException("Direct-delete plan approval hash is invalid.");
    }

    private static void ValidateDirectDeletePlan(
        VideoVariantLooseCleanupApplyManifest manifest,
        VideoVariantLooseCleanupDirectDeletePlan plan,
        string approval
    )
    {
        ValidateManifestStructure(manifest, verifyHash: true);
        ValidateDirectDeletePlanStructure(plan);
        if (!approval.Equals(plan.ApprovalHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Approval hash does not match direct-delete plan.");
        if (
            plan.SourceManifestApprovalHash != manifest.ApprovalHash
            || plan.SourceOperationSetHash != manifest.OperationSetHash
            || plan.DataConfigSha256 != manifest.DataConfigSha256
            || plan.OperationIndices.Any(index => index >= manifest.Operations.Count)
        )
            throw new InvalidOperationException("Direct-delete plan does not match manifest.");
    }

    private static VideoVariantLooseCleanupJournal LoadAndValidateSourceJournal(
        VideoVariantLooseCleanupApplyManifest manifest,
        byte[] bytes,
        IReadOnlyList<ApplyWork> work
    )
    {
        VideoVariantLooseCleanupJournal journal =
            JsonSerializer.Deserialize<VideoVariantLooseCleanupJournal>(bytes, JsonOptions)
            ?? throw new InvalidOperationException("Invalid source apply journal.");
        if (journal.BatchSize <= 0)
            throw new InvalidOperationException("Source apply journal batch size is invalid.");
        int expectedBatches = (work.Count + journal.BatchSize - 1) / journal.BatchSize;
        if (
            journal.ApprovalHash != manifest.ApprovalHash
            || journal.OperationSetHash != manifest.OperationSetHash
            || journal.Batches.Count != expectedBatches
            || !journal
                .Batches.Select(batch => batch.Index)
                .SequenceEqual(Enumerable.Range(0, expectedBatches))
            || journal.Batches.Any(batch =>
                batch.State
                    is not ("pending" or "moving" or "quarantined" or "db-committed" or "verified")
            )
        )
            throw new InvalidOperationException("Source apply journal is invalid or mismatched.");

        foreach (VideoVariantLooseCleanupJournalBatch batch in journal.Batches)
        {
            ApplyWork[] batchWork = work.Skip(batch.Index * journal.BatchSize)
                .Take(journal.BatchSize)
                .ToArray();
            bool quarantined = batch.State is "quarantined" or "db-committed" or "verified";
            int originals = quarantined ? batchWork.Length : 0;
            int direct = quarantined ? batchWork.Count(item => item.Operation.DirectPresent) : 0;
            long bytesQuarantined = quarantined
                ? batchWork.Sum(item =>
                    item.Operation.OriginalSize
                    + (item.Operation.DirectPresent ? item.Operation.DirectSize : 0)
                )
                : 0;
            bool databaseCommitted = batch.State is "db-committed" or "verified";
            if (
                batch.OriginalsQuarantined != originals
                || batch.DirectQuarantined != direct
                || batch.BytesQuarantined != bytesQuarantined
                || batch.PrimaryDeleted < 0
                || batch.IncrementalDeleted < 0
                || (
                    !databaseCommitted
                    && (batch.PrimaryDeleted != 0 || batch.IncrementalDeleted != 0)
                )
            )
                throw new InvalidOperationException("Source apply journal counters are invalid.");
        }
        return journal;
    }

    private static void ValidateDirectDeleteSourceJournal(
        VideoVariantLooseCleanupApplyManifest manifest,
        VideoVariantLooseCleanupDirectDeletePlan plan,
        byte[] bytes,
        IReadOnlyList<ApplyWork> work
    )
    {
        VideoVariantLooseCleanupJournal journal = LoadAndValidateSourceJournal(
            manifest,
            bytes,
            work
        );
        if (
            Sha256(bytes) != plan.SourceJournalSha256
            || journal.RunId != plan.SourceJournalRunId
            || journal.BatchSize != plan.SourceJournalBatchSize
            || journal.Batches.Count != plan.SourceJournalBatchCount
        )
            throw new InvalidOperationException("Frozen source apply journal changed.");
        HashSet<int> pending = journal
            .Batches.Where(batch => batch.State == "pending")
            .SelectMany(batch =>
                Enumerable.Range(
                    batch.Index * journal.BatchSize,
                    Math.Min(journal.BatchSize, work.Count - batch.Index * journal.BatchSize)
                )
            )
            .ToHashSet();
        if (plan.OperationIndices.Any(index => !pending.Contains(index)))
            throw new InvalidOperationException("Plan contains non-pending source operations.");
    }

    private static VideoVariantLooseCleanupDirectDeleteJournal LoadOrCreateDirectDeleteJournal(
        VideoVariantLooseCleanupDirectDeletePlan plan,
        string path,
        int batchSize,
        int batchCount,
        bool resume,
        IReadOnlyList<ApplyWork> work
    )
    {
        if (File.Exists(path))
        {
            if (!resume)
                throw new InvalidOperationException("Existing journal requires --resume.");
            VideoVariantLooseCleanupDirectDeleteJournal existing =
                JsonSerializer.Deserialize<VideoVariantLooseCleanupDirectDeleteJournal>(
                    File.ReadAllBytes(path),
                    JsonOptions
                ) ?? throw new InvalidOperationException("Invalid direct-delete journal.");
            if (
                existing.Version != 1
                || existing.Mode != "direct-delete-loose-v1"
                || existing.PlanApprovalHash != plan.ApprovalHash
                || existing.BatchSize != batchSize
                || existing.Batches.Count != batchCount
                || !existing
                    .Batches.Select(batch => batch.Index)
                    .SequenceEqual(Enumerable.Range(0, batchCount))
                || CalculateDirectDeleteJournalHash(existing) != existing.JournalHash
            )
                throw new InvalidOperationException("Direct-delete journal does not match plan.");
            ValidateDirectDeleteJournal(existing, work);
            return existing;
        }
        if (resume)
            throw new InvalidOperationException("Resume requested but journal does not exist.");
        VideoVariantLooseCleanupDirectDeleteJournal journal = new()
        {
            Version = 1,
            Mode = "direct-delete-loose-v1",
            PlanApprovalHash = plan.ApprovalHash,
            RunId = Guid.NewGuid(),
            StartedUtc = DateTimeOffset.UtcNow,
            BatchSize = batchSize,
            Batches = Enumerable
                .Range(0, batchCount)
                .Select(index => new VideoVariantLooseCleanupDirectDeleteJournalBatch
                {
                    Index = index,
                })
                .ToList(),
        };
        WriteDirectDeleteJournal(path, journal, work);
        return journal;
    }

    private static void WriteDirectDeleteJournal(
        string path,
        VideoVariantLooseCleanupDirectDeleteJournal journal,
        IReadOnlyList<ApplyWork> work
    )
    {
        ValidateDirectDeleteJournal(journal, work);
        journal.JournalHash = CalculateDirectDeleteJournalHash(journal);
        WriteJsonAtomic(path, journal);
    }

    private static void ValidateDirectDeleteJournal(
        VideoVariantLooseCleanupDirectDeleteJournal journal,
        IReadOnlyList<ApplyWork> work
    )
    {
        int previousRank = int.MaxValue;

        for (int index = 0; index < journal.Batches.Count; index++)
        {
            VideoVariantLooseCleanupDirectDeleteJournalBatch batch = journal.Batches[index];
            int rank = batch.State switch
            {
                "pending" => 0,
                "deleting-files" => 1,
                "files-absent" => 2,
                "db-deleting" => 3,
                "db-committed" => 4,
                "verified" => 5,
                _ => throw new InvalidOperationException("Direct-delete journal state is invalid."),
            };
            if (rank > previousRank)
                throw new InvalidOperationException(
                    "Direct-delete journal state order is invalid."
                );
            previousRank = rank;
            ApplyWork[] batchWork = work.Skip(index * journal.BatchSize)
                .Take(journal.BatchSize)
                .ToArray();
            int files = batchWork.Sum(item => 1 + (item.Operation.DirectPresent ? 1 : 0));
            long bytes = batchWork.Sum(item =>
                item.Operation.OriginalSize
                + (item.Operation.DirectPresent ? item.Operation.DirectSize : 0)
            );
            bool filesRecorded = rank >= 2;
            if (
                batch.Index != index
                || batch.FilesDeleted != (filesRecorded ? files : 0)
                || batch.BytesDeleted != (filesRecorded ? bytes : 0)
                || batch.RowsDeleted < 0
                || batch.RowsDeleted > batchWork.Length
                || (rank < 4 && batch.RowsDeleted != 0)
                || (
                    rank >= 4 && batch.RowsDeleted is not 0 && batch.RowsDeleted != batchWork.Length
                )
            )
                throw new InvalidOperationException("Direct-delete journal counters are invalid.");
        }
    }

    private static void ValidateDirectDeleteFiles(
        IEnumerable<ApplyWork> work,
        bool requireActiveFiles,
        int physicalParallelism,
        CancellationToken token
    ) =>
        RunPhysical(
            work,
            physicalParallelism,
            token,
            item =>
            {
                if (File.Exists(item.OriginalQuarantinePath))
                    throw new InvalidOperationException(
                        $"Quarantine destination exists: {item.OriginalQuarantinePath}"
                    );
                if (requireActiveFiles)
                    RequireFile(
                        item.OriginalPath,
                        item.Operation.OriginalSize,
                        item.Operation.OriginalLastWriteUtcTicks,
                        "Original file"
                    );
                else if (File.Exists(item.OriginalPath))
                    RequireFile(
                        item.OriginalPath,
                        item.Operation.OriginalSize,
                        item.Operation.OriginalLastWriteUtcTicks,
                        "Original file"
                    );
                if (File.Exists(item.DirectQuarantinePath))
                    throw new InvalidOperationException(
                        $"Quarantine destination exists: {item.DirectQuarantinePath}"
                    );
                if (item.Operation.DirectPresent)
                {
                    if (requireActiveFiles)
                        RequireFile(
                            item.DirectPath,
                            item.Operation.DirectSize,
                            item.Operation.DirectLastWriteUtcTicks,
                            "Direct file"
                        );
                    else if (File.Exists(item.DirectPath))
                        RequireFile(
                            item.DirectPath,
                            item.Operation.DirectSize,
                            item.Operation.DirectLastWriteUtcTicks,
                            "Direct file"
                        );
                }
                else if (File.Exists(item.DirectPath))
                    throw new InvalidOperationException(
                        $"Unexpected direct file: {item.LogicalPath}"
                    );
            }
        );

    private static void DeleteDirectDeleteFiles(
        IEnumerable<ApplyWork> work,
        int physicalParallelism,
        CancellationToken token
    ) =>
        RunPhysical(
            work,
            physicalParallelism,
            token,
            item =>
            {
                if (File.Exists(item.OriginalQuarantinePath))
                    throw new InvalidOperationException(
                        $"Quarantine destination exists: {item.OriginalQuarantinePath}"
                    );
                if (File.Exists(item.DirectQuarantinePath))
                    throw new InvalidOperationException(
                        $"Quarantine destination exists: {item.DirectQuarantinePath}"
                    );
                if (!item.Operation.DirectPresent && File.Exists(item.DirectPath))
                    throw new InvalidOperationException(
                        $"Unexpected direct file: {item.LogicalPath}"
                    );
                DeleteIfMatching(
                    item.OriginalPath,
                    item.Operation.OriginalSize,
                    item.Operation.OriginalLastWriteUtcTicks,
                    "Original file"
                );
                if (item.Operation.DirectPresent)
                    DeleteIfMatching(
                        item.DirectPath,
                        item.Operation.DirectSize,
                        item.Operation.DirectLastWriteUtcTicks,
                        "Direct file"
                    );
            }
        );

    private static void DeleteIfMatching(string path, long size, long ticks, string label)
    {
        if (!File.Exists(path))
            return;
        RequireFile(path, size, ticks, label);
        File.Delete(path);
    }

    private static void VerifyDirectDeleteFiles(
        IEnumerable<ApplyWork> work,
        int physicalParallelism,
        CancellationToken token
    ) =>
        RunPhysical(
            work,
            physicalParallelism,
            token,
            item =>
            {
                if (File.Exists(item.OriginalPath) || File.Exists(item.OriginalQuarantinePath))
                    throw new InvalidOperationException(
                        $"Original or quarantine file remains: {item.LogicalPath}"
                    );
                if (File.Exists(item.DirectPath) || File.Exists(item.DirectQuarantinePath))
                    throw new InvalidOperationException(
                        $"Direct or quarantine file remains: {item.LogicalPath}"
                    );
            }
        );

    private static void ValidateDirectDeleteKeeps(
        VideoVariantLooseCleanupDirectDeletePlan plan,
        CancellationToken token
    )
    {
        foreach (VideoVariantLooseCleanupDirectDeleteKeep keep in plan.Keeps)
        {
            token.ThrowIfCancellationRequested();
            RequireFile(keep.Path, keep.Size, keep.LastWriteUtcTicks, "Keep file");
        }
    }

    private static void RunPhysical(
        IEnumerable<ApplyWork> work,
        int physicalParallelism,
        CancellationToken token,
        Action<ApplyWork> action
    )
    {
        if (physicalParallelism == 1)
        {
            foreach (ApplyWork item in work)
            {
                token.ThrowIfCancellationRequested();
                action(item);
            }
            return;
        }
        Parallel.ForEach(
            work,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = physicalParallelism,
                CancellationToken = token,
            },
            action
        );
    }

    private static async Task<IReadOnlyDictionary<string, int>> LoadRowCounts(
        IVideoVariantLooseCleanupDatabase database,
        VideoVariantLooseCleanupApplyManifest manifest,
        IEnumerable<ApplyWork> work,
        CancellationToken token
    )
    {
        string[] paths = work.Select(item => NormalizePath(item.LogicalPath)).ToArray();
        IReadOnlyDictionary<string, int> counts = await database.LoadRowCountsAsync(
            manifest.PrimaryNamespace,
            manifest.IncrementalNamespaces,
            paths,
            token
        );
        if (
            counts.Count != paths.Length
            || paths.Any(path => !counts.ContainsKey(path))
            || counts.Any(value => value.Value < 0)
        )
            throw new InvalidOperationException(
                "Database returned invalid direct-delete row counts."
            );
        return counts;
    }

    private static async Task RequireExactRowCounts(
        IVideoVariantLooseCleanupDatabase database,
        VideoVariantLooseCleanupApplyManifest manifest,
        IEnumerable<ApplyWork> work,
        int expected,
        CancellationToken token
    )
    {
        IReadOnlyDictionary<string, int> counts = await LoadRowCounts(
            database,
            manifest,
            work,
            token
        );
        string? invalid = counts.FirstOrDefault(value => value.Value != expected).Key;
        if (invalid is not null)
            throw new InvalidOperationException(
                $"Expected {expected} cache row(s) for direct-delete path: {invalid}"
            );
    }

    private VideoVariantLooseCleanupDirectDeleteReceipt LoadDirectDeleteReceipt(string path)
    {
        VideoVariantLooseCleanupDirectDeleteReceipt receipt =
            JsonSerializer.Deserialize<VideoVariantLooseCleanupDirectDeleteReceipt>(
                File.ReadAllBytes(path),
                JsonOptions
            ) ?? throw new InvalidOperationException("Invalid direct-delete receipt.");
        if (CalculateDirectDeleteReceiptHash(receipt) != receipt.ReceiptHash)
            throw new InvalidOperationException("Direct-delete receipt hash is invalid.");
        return receipt;
    }

    private static void ValidateDirectDeleteReceipt(
        VideoVariantLooseCleanupApplyManifest manifest,
        VideoVariantLooseCleanupDirectDeletePlan plan,
        VideoVariantLooseCleanupDirectDeleteReceipt receipt
    )
    {
        if (
            receipt.Version != 1
            || receipt.PlanApprovalHash != plan.ApprovalHash
            || receipt.SourceManifestApprovalHash != manifest.ApprovalHash
            || receipt.SourceOperationSetHash != manifest.OperationSetHash
            || receipt.SourceJournalSha256 != plan.SourceJournalSha256
            || receipt.SourceJournalRunId != plan.SourceJournalRunId
            || receipt.SourceJournalBatchSize != plan.SourceJournalBatchSize
            || receipt.SourceJournalBatchCount != plan.SourceJournalBatchCount
            || receipt.Operations != plan.Operations
            || receipt.BytesDeleted != plan.Bytes
            || receipt.RowsDeleted != plan.Operations
            || receipt.FilesDeleted != plan.Files
            || receipt.Batches <= 0
        )
            throw new InvalidOperationException("Direct-delete receipt does not match plan.");
    }

    private IVideoVariantLooseCleanupDatabase Database(string connectionString) =>
        _database ?? new NpgsqlVideoVariantLooseCleanupDatabase(connectionString);

    private static string StoreNamespace(string storeId, string cacheId) =>
        $"backup:media-cache:{storeId}:{cacheId}";

    private static string RequireLocalOutputPath(string path, string name)
    {
        string full = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(full);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"{name} directory does not exist: {directory}");
        return full;
    }

    private static void RequireSameVolume(string source, string destination)
    {
        if (
            !string.Equals(
                Path.GetPathRoot(source),
                Path.GetPathRoot(destination),
                PhysicalPathSemantics.Comparison
            )
        )
            throw new InvalidOperationException(
                "Source and quarantine destination must use same volume."
            );
    }

    private static string FindQuarantineRoot(string path, string approval)
    {
        string suffix = Path.Combine(QuarantineDirectory, approval);
        int index = path.IndexOf(suffix, PhysicalPathSemantics.Comparison);
        return index < 0 ? path : path[..(index + suffix.Length)];
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string NormalizePath(string path) => path.Replace('\\', '/').ToLowerInvariant();

    private static void WriteJsonAtomic<T>(string path, T value)
    {
        string full = RequireLocalOutputPath(path, nameof(path));
        string temp = $"{full}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));
            File.Move(temp, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    private sealed record ApplyWork(
        VideoVariantLooseCleanupApplyOperation Operation,
        string LogicalPath,
        string KeepPath,
        string OriginalPath,
        string DirectPath,
        string OriginalQuarantinePath,
        string DirectQuarantinePath
    );
}

internal interface IVideoVariantLooseCleanupDatabase
{
    Task<VideoVariantLooseCleanupNamespaces> DiscoverNamespacesAsync(
        string storeNamespace,
        CancellationToken token
    );
    Task<IReadOnlyDictionary<string, int>> LoadPartitionIdsAsync(
        string primaryNamespace,
        IReadOnlyCollection<string> normalizedPaths,
        CancellationToken token
    );
    Task<VideoVariantLooseCleanupIndexStatus> CheckIndexesAsync(CancellationToken token);
    Task CreateIndexesAsync(CancellationToken token);
    Task<IAsyncDisposable?> TryAcquireLockAsync(string storeNamespace, CancellationToken token);
    Task<VideoVariantLooseCleanupDeleteResult> DeleteBatchAsync(
        string primaryNamespace,
        IReadOnlyList<string> incrementalNamespaces,
        IReadOnlyList<string> normalizedPaths,
        CancellationToken token
    );
    Task<bool> AreRowsAbsentAsync(
        string primaryNamespace,
        IReadOnlyList<string> incrementalNamespaces,
        IReadOnlyList<string> normalizedPaths,
        CancellationToken token
    );
    Task<IReadOnlyDictionary<string, int>> LoadRowCountsAsync(
        string primaryNamespace,
        IReadOnlyList<string> incrementalNamespaces,
        IReadOnlyList<string> normalizedPaths,
        CancellationToken token
    );
}

internal sealed class NpgsqlVideoVariantLooseCleanupDatabase(string connectionString)
    : IVideoVariantLooseCleanupDatabase
{
    private readonly string _connectionString = WithKeepAlive(connectionString);
    private NpgsqlConnection? _lockedConnection;

    public async Task<VideoVariantLooseCleanupNamespaces> DiscoverNamespacesAsync(
        string storeNamespace,
        CancellationToken token
    )
    {
        await using NpgsqlConnection connection = await Open(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            token
        );
        await using NpgsqlCommand readOnly = new(
            "SET TRANSACTION READ ONLY",
            connection,
            transaction
        );
        await readOnly.ExecuteNonQueryAsync(token);
        string escapedStoreNamespace = EscapeLike(storeNamespace);
        List<string> primary = await ReadNamespaces(
            connection,
            transaction,
            escapedStoreNamespace + "|primary|%",
            token
        );
        List<string> incremental = await ReadNamespaces(
            connection,
            transaction,
            escapedStoreNamespace + "|incremental|%",
            token,
            "media_cache_incremental_entries"
        );
        await transaction.CommitAsync(token);
        if (primary.Count != 1)
            throw new InvalidOperationException(
                $"Expected exactly one primary cache namespace; found {primary.Count}."
            );
        return new(primary[0], incremental);
    }

    public async Task<IReadOnlyDictionary<string, int>> LoadPartitionIdsAsync(
        string primaryNamespace,
        IReadOnlyCollection<string> normalizedPaths,
        CancellationToken token
    )
    {
        HashSet<string> requested = normalizedPaths.ToHashSet(StringComparer.Ordinal);
        if (requested.Count != normalizedPaths.Count)
            throw new InvalidOperationException("Keep paths must be unique and normalized.");
        if (requested.Any(path => path != NormalizePath(path)))
            throw new InvalidOperationException("Keep paths must be unique and normalized.");
        if (requested.Count == 0)
            return new Dictionary<string, int>(StringComparer.Ordinal);

        Dictionary<string, int> result = new(StringComparer.Ordinal);
        await using NpgsqlConnection connection = await Open(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            token
        );

        await using (
            NpgsqlCommand readOnly = new("SET TRANSACTION READ ONLY", connection, transaction)
        )
            await readOnly.ExecuteNonQueryAsync(token);

        await using (
            NpgsqlCommand command = new(
                "SELECT lower(replace(path, chr(92), '/')), partition_id FROM media_cache_primary_entries WHERE namespace = @namespace AND lower(replace(path, chr(92), '/')) = ANY(@paths)",
                connection,
                transaction
            )
        )
        {
            command.CommandTimeout = 0;
            command.Parameters.AddWithValue("namespace", primaryNamespace);
            command.Parameters.AddWithValue("paths", requested.ToArray());
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                token
            );

            while (await reader.ReadAsync(token))
            {
                string path = reader.GetString(0).ToLowerInvariant();
                if (!requested.Contains(path))
                    continue;
                if (reader.IsDBNull(1))
                    throw new InvalidOperationException($"Keep path has null partition ID: {path}");
                if (!result.TryAdd(path, reader.GetInt32(1)))
                    throw new InvalidOperationException(
                        $"Keep path has duplicate database rows: {path}"
                    );
            }
        }
        await transaction.CommitAsync(token);
        string? missing = requested.FirstOrDefault(path => !result.ContainsKey(path));
        if (missing is not null)
            throw new InvalidOperationException(
                $"Keep path is missing from primary cache: {missing}"
            );
        return result;
    }

    public async Task<VideoVariantLooseCleanupIndexStatus> CheckIndexesAsync(
        CancellationToken token
    )
    {
        NpgsqlConnection connection = _lockedConnection ?? await Open(token);
        try
        {
            const string sql = """
                SELECT c.relname, i.indisvalid, x.indexdef
                FROM pg_index i
                JOIN pg_class c ON c.oid = i.indexrelid
                JOIN pg_namespace n ON n.oid = c.relnamespace
                JOIN pg_indexes x ON x.schemaname = n.nspname AND x.indexname = c.relname
                WHERE c.relname = ANY(@names)
                ORDER BY c.relname
                """;
            await using NpgsqlCommand command = new(sql, connection);
            command.Parameters.AddWithValue(
                "names",
                new[]
                {
                    VideoVariantLooseCleanupApplyService.PrimaryIndexName,
                    VideoVariantLooseCleanupApplyService.IncrementalIndexName,
                }
            );
            Dictionary<string, (bool Valid, string Definition)> values = [];
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
                values[reader.GetString(0)] = (reader.GetBoolean(1), reader.GetString(2));
            return new(
                IsExpected(
                    values,
                    VideoVariantLooseCleanupApplyService.PrimaryIndexName,
                    "media_cache_primary_entries"
                ),
                IsExpected(
                    values,
                    VideoVariantLooseCleanupApplyService.IncrementalIndexName,
                    "media_cache_incremental_entries"
                )
            );
        }
        finally
        {
            if (_lockedConnection is null)
                await connection.DisposeAsync();
        }
    }

    public async Task CreateIndexesAsync(CancellationToken token)
    {
        await using NpgsqlConnection connection = await Open(token);
        Dictionary<string, bool> validity = await ReadIndexValidity(connection, token);
        if (
            validity.TryGetValue(
                VideoVariantLooseCleanupApplyService.PrimaryIndexName,
                out bool primaryValid
            ) && !primaryValid
        )
            await ExecuteIndexCommand(
                connection,
                VideoVariantLooseCleanupApplyService.DropInvalidPrimaryIndexSql,
                token
            );
        if (
            validity.TryGetValue(
                VideoVariantLooseCleanupApplyService.IncrementalIndexName,
                out bool incrementalValid
            ) && !incrementalValid
        )
            await ExecuteIndexCommand(
                connection,
                VideoVariantLooseCleanupApplyService.DropInvalidIncrementalIndexSql,
                token
            );

        foreach (
            string sql in new[]
            {
                VideoVariantLooseCleanupApplyService.CreatePrimaryIndexSql,
                VideoVariantLooseCleanupApplyService.CreateIncrementalIndexSql,
            }
        )
            await ExecuteIndexCommand(connection, sql, token);
    }

    private static async Task<Dictionary<string, bool>> ReadIndexValidity(
        NpgsqlConnection connection,
        CancellationToken token
    )
    {
        const string sql = """
            SELECT c.relname, i.indisvalid
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indexrelid
            WHERE c.relname = ANY(@names)
            """;
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue(
            "names",
            new[]
            {
                VideoVariantLooseCleanupApplyService.PrimaryIndexName,
                VideoVariantLooseCleanupApplyService.IncrementalIndexName,
            }
        );
        Dictionary<string, bool> values = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);

        while (await reader.ReadAsync(token))
            values[reader.GetString(0)] = reader.GetBoolean(1);
        return values;
    }

    private static async Task ExecuteIndexCommand(
        NpgsqlConnection connection,
        string sql,
        CancellationToken token
    )
    {
        await using NpgsqlCommand command = new(sql, connection) { CommandTimeout = 0 };
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task<IAsyncDisposable?> TryAcquireLockAsync(
        string storeNamespace,
        CancellationToken token
    )
    {
        if (_lockedConnection is not null)
            throw new InvalidOperationException("Advisory lock session is already open.");
        NpgsqlConnection connection = await Open(token);
        await using NpgsqlCommand command = new(
            "SELECT pg_try_advisory_lock(hashtextextended('media-cache:' || @storeNamespace, 0))",
            connection
        );
        command.Parameters.AddWithValue("storeNamespace", storeNamespace);
        if (await command.ExecuteScalarAsync(token) is not true)
        {
            await connection.DisposeAsync();
            return null;
        }
        _lockedConnection = connection;
        return new AdvisoryLease(this, connection, storeNamespace);
    }

    public async Task<VideoVariantLooseCleanupDeleteResult> DeleteBatchAsync(
        string primaryNamespace,
        IReadOnlyList<string> incrementalNamespaces,
        IReadOnlyList<string> normalizedPaths,
        CancellationToken token
    )
    {
        NpgsqlConnection connection = LockedConnection();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        try
        {
            await using (
                NpgsqlCommand create = new(
                    "CREATE TEMP TABLE loose_cleanup_paths (normalized_path TEXT PRIMARY KEY) ON COMMIT DROP",
                    connection,
                    transaction
                )
            )
                await create.ExecuteNonQueryAsync(token);

            await using (
                NpgsqlBinaryImporter importer = await connection.BeginBinaryImportAsync(
                    "COPY loose_cleanup_paths (normalized_path) FROM STDIN (FORMAT BINARY)",
                    token
                )
            )
            {
                foreach (string path in normalizedPaths)
                {
                    await importer.StartRowAsync(token);
                    await importer.WriteAsync(
                        path.Replace('\\', '/').ToLowerInvariant(),
                        NpgsqlDbType.Text,
                        token
                    );
                }
                await importer.CompleteAsync(token);
            }
            int primary = await Delete(
                connection,
                transaction,
                "media_cache_primary_entries",
                primaryNamespace,
                token
            );
            int incremental = 0;

            foreach (string cacheNamespace in incrementalNamespaces)
                incremental += await Delete(
                    connection,
                    transaction,
                    "media_cache_incremental_entries",
                    cacheNamespace,
                    token
                );
            await transaction.CommitAsync(token);
            return new(primary, incremental);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> AreRowsAbsentAsync(
        string primaryNamespace,
        IReadOnlyList<string> incrementalNamespaces,
        IReadOnlyList<string> normalizedPaths,
        CancellationToken token
    )
    {
        NpgsqlConnection connection = _lockedConnection ?? await Open(token);
        try
        {
            const string sql = """
                SELECT NOT EXISTS (
                    SELECT 1 FROM media_cache_primary_entries
                    WHERE namespace = @primary AND lower(replace(path, chr(92), '/')) = ANY(@paths)
                    UNION ALL
                    SELECT 1 FROM media_cache_incremental_entries
                    WHERE namespace = ANY(@incrementals) AND lower(replace(path, chr(92), '/')) = ANY(@paths)
                )
                """;
            await using NpgsqlCommand command = new(sql, connection);
            command.Parameters.AddWithValue("primary", primaryNamespace);
            command.Parameters.AddWithValue("incrementals", incrementalNamespaces.ToArray());
            command.Parameters.AddWithValue(
                "paths",
                normalizedPaths
                    .Select(value => value.Replace('\\', '/').ToLowerInvariant())
                    .ToArray()
            );
            return await command.ExecuteScalarAsync(token) is true;
        }
        finally
        {
            if (_lockedConnection is null)
                await connection.DisposeAsync();
        }
    }

    public async Task<IReadOnlyDictionary<string, int>> LoadRowCountsAsync(
        string primaryNamespace,
        IReadOnlyList<string> incrementalNamespaces,
        IReadOnlyList<string> normalizedPaths,
        CancellationToken token
    )
    {
        string[] paths = normalizedPaths
            .Select(NormalizePath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        NpgsqlConnection connection = _lockedConnection ?? await Open(token);
        try
        {
            const string sql = """
                SELECT normalized_path, count(*)::int
                FROM (
                    SELECT lower(replace(path, chr(92), '/')) AS normalized_path
                    FROM media_cache_primary_entries
                    WHERE namespace = @primary AND lower(replace(path, chr(92), '/')) = ANY(@paths)
                    UNION ALL
                    SELECT lower(replace(path, chr(92), '/')) AS normalized_path
                    FROM media_cache_incremental_entries
                    WHERE namespace = ANY(@incrementals) AND lower(replace(path, chr(92), '/')) = ANY(@paths)
                ) rows
                GROUP BY normalized_path
                """;
            await using NpgsqlCommand command = new(sql, connection);
            command.Parameters.AddWithValue("primary", primaryNamespace);
            command.Parameters.AddWithValue("incrementals", incrementalNamespaces.ToArray());
            command.Parameters.AddWithValue("paths", paths);
            Dictionary<string, int> counts = paths.ToDictionary(
                path => path,
                _ => 0,
                StringComparer.Ordinal
            );
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
                counts[reader.GetString(0)] = reader.GetInt32(1);
            return counts;
        }
        finally
        {
            if (_lockedConnection is null)
                await connection.DisposeAsync();
        }
    }

    private async Task<NpgsqlConnection> Open(CancellationToken token)
    {
        NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(token);
        return connection;
    }

    private static string WithKeepAlive(string value)
    {
        NpgsqlConnectionStringBuilder builder = new(value);
        if (builder.KeepAlive == 0)
            builder.KeepAlive = 30;
        return builder.ConnectionString;
    }

    private NpgsqlConnection LockedConnection() =>
        _lockedConnection
        ?? throw new InvalidOperationException("Advisory lock session is not open.");

    private static async Task<List<string>> ReadNamespaces(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string prefix,
        CancellationToken token,
        string table = "media_cache_primary_entries"
    )
    {
        await using NpgsqlCommand command = new(
            $"SELECT DISTINCT namespace FROM {table} WHERE namespace LIKE @prefix ESCAPE '\\' ORDER BY namespace",
            connection,
            transaction
        );
        command.Parameters.AddWithValue("prefix", prefix);
        List<string> values = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);

        while (await reader.ReadAsync(token))
            values.Add(reader.GetString(0));
        return values;
    }

    private static string EscapeLike(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static bool IsExpected(
        Dictionary<string, (bool Valid, string Definition)> values,
        string name,
        string table
    ) =>
        values.TryGetValue(name, out var value)
        && value.Valid
        && value.Definition.Contains($" ON public.{table} ", StringComparison.OrdinalIgnoreCase)
        && value.Definition.Contains("namespace", StringComparison.OrdinalIgnoreCase)
        && value.Definition.Contains(
            "lower(replace(path, chr(92), '/'",
            StringComparison.OrdinalIgnoreCase
        );

    private static async Task<int> Delete(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string table,
        string cacheNamespace,
        CancellationToken token
    )
    {
        await using NpgsqlCommand command = new(
            $"""
            DELETE FROM {table} target
            USING loose_cleanup_paths paths
            WHERE target.namespace = @namespace
              AND lower(replace(target.path, chr(92), '/')) = paths.normalized_path
            """,
            connection,
            transaction
        );
        command.Parameters.AddWithValue("namespace", cacheNamespace);
        return await command.ExecuteNonQueryAsync(token);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/').ToLowerInvariant();

    private sealed class AdvisoryLease(
        NpgsqlVideoVariantLooseCleanupDatabase owner,
        NpgsqlConnection connection,
        string storeNamespace
    ) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (connection.State != ConnectionState.Open)
                    return;
                await using NpgsqlCommand command = new(
                    "SELECT pg_advisory_unlock(hashtextextended('media-cache:' || @storeNamespace, 0))",
                    connection
                );
                command.Parameters.AddWithValue("storeNamespace", storeNamespace);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                owner._lockedConnection = null;
                await connection.DisposeAsync();
            }
        }
    }
}

public sealed record VideoVariantLooseCleanupApplyManifest
{
    public int Version { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public string SourceManifestApprovalHash { get; init; } = string.Empty;
    public string DataConfigPath { get; init; } = string.Empty;
    public long DataConfigLength { get; init; }
    public long DataConfigLastWriteUtcTicks { get; init; }
    public string DataConfigSha256 { get; init; } = string.Empty;
    public string MediaStoreId { get; init; } = string.Empty;
    public string CacheId { get; init; } = string.Empty;
    public string PrimaryNamespace { get; init; } = string.Empty;
    public List<string> IncrementalNamespaces { get; init; } = [];
    public string DirectRoot { get; init; } = string.Empty;
    public List<string> Paths { get; init; } = [];
    public List<VideoVariantLooseCleanupApplyOperation> Operations { get; set; } = [];
    public string OperationSetHash { get; set; } = string.Empty;
    public string ApprovalHash { get; set; } = string.Empty;
}

public sealed class VideoVariantLooseCleanupApplyOperation
{
    public int RemovePathIndex { get; init; }
    public int KeepPathIndex { get; init; }
    public int PartitionId { get; init; }
    public int KeepPartitionId { get; init; }
    public long OriginalSize { get; init; }
    public long OriginalLastWriteUtcTicks { get; init; }
    public bool DirectPresent { get; init; }
    public long DirectSize { get; init; }
    public long DirectLastWriteUtcTicks { get; init; }
}

public sealed record VideoVariantLooseCleanupNamespaces(
    string PrimaryNamespace,
    List<string> IncrementalNamespaces
);

public sealed record VideoVariantLooseCleanupIndexStatus(bool PrimaryValid, bool IncrementalValid)
{
    public bool IsValid => PrimaryValid && IncrementalValid;
}

public sealed record VideoVariantLooseCleanupDeleteResult(
    int PrimaryDeleted,
    int IncrementalDeleted
);

public sealed record VideoVariantLooseCleanupApplyProgress(
    int CompletedBatches,
    int TotalBatches,
    int BatchOperations,
    int TotalOperations
);

public sealed record VideoVariantLooseCleanupReceipt
{
    public int Version { get; init; }
    public string ApprovalHash { get; init; } = string.Empty;
    public string OperationSetHash { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceJournalSha256 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? OperationIndices { get; init; }
    public string Host { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public Guid RunId { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset CompletedUtc { get; init; }
    public int Operations { get; init; }
    public int OriginalsQuarantined { get; init; }
    public int DirectQuarantined { get; init; }
    public long BytesQuarantined { get; init; }
    public int PrimaryDeleted { get; init; }
    public int IncrementalDeleted { get; init; }
    public string PrimaryNamespace { get; init; } = string.Empty;
    public List<string> IncrementalNamespaces { get; init; } = [];
    public List<string> QuarantineRoots { get; init; } = [];
    public int Batches { get; init; }
    public string ReceiptHash { get; set; } = string.Empty;
}

internal sealed class VideoVariantLooseCleanupJournal
{
    public string ApprovalHash { get; init; } = string.Empty;
    public string OperationSetHash { get; init; } = string.Empty;
    public Guid RunId { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public int BatchSize { get; init; }
    public List<VideoVariantLooseCleanupJournalBatch> Batches { get; init; } = [];
}

internal sealed class VideoVariantLooseCleanupJournalBatch
{
    public int Index { get; init; }
    public string State { get; set; } = "pending";
    public int OriginalsQuarantined { get; set; }
    public int DirectQuarantined { get; set; }
    public long BytesQuarantined { get; set; }
    public int PrimaryDeleted { get; set; }
    public int IncrementalDeleted { get; set; }
}

internal sealed record VideoVariantLooseCleanupReconciliationJournal
{
    public int Version { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string ManifestApprovalHash { get; init; } = string.Empty;
    public string OperationSetHash { get; init; } = string.Empty;
    public string SourceJournalSha256 { get; init; } = string.Empty;
    public Guid SourceJournalRunId { get; init; }
    public Guid RunId { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public List<VideoVariantLooseCleanupReconciliationBatch> Batches { get; init; } = [];
    public string JournalHash { get; set; } = string.Empty;
}

internal sealed class VideoVariantLooseCleanupReconciliationBatch
{
    public int SourceBatchIndex { get; init; }
    public string SourceState { get; init; } = string.Empty;
    public int SourceOriginalsQuarantined { get; init; }
    public int SourceDirectQuarantined { get; init; }
    public long SourceBytesQuarantined { get; init; }
    public int SourcePrimaryDeleted { get; init; }
    public int SourceIncrementalDeleted { get; init; }
    public int StartOperationIndex { get; init; }
    public int OperationCount { get; init; }
    public string State { get; set; } = "pending";
    public int PrimaryDeleted { get; set; }
    public int IncrementalDeleted { get; set; }
}

internal static class PhysicalPathSemantics
{
    internal static StringComparer Comparer => ComparerFor(OperatingSystem.IsWindows());
    internal static StringComparison Comparison => ComparisonFor(OperatingSystem.IsWindows());

    internal static StringComparer ComparerFor(bool windows) =>
        windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static StringComparison ComparisonFor(bool windows) =>
        windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

public sealed record VideoVariantLooseCleanupDirectDeletePlan
{
    public int Version { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public string SourceManifestApprovalHash { get; init; } = string.Empty;
    public string SourceOperationSetHash { get; init; } = string.Empty;
    public string SourceJournalSha256 { get; init; } = string.Empty;
    public Guid SourceJournalRunId { get; init; }
    public int SourceJournalBatchSize { get; init; }
    public int SourceJournalBatchCount { get; init; }
    public string DataConfigSha256 { get; init; } = string.Empty;
    public List<int> OperationIndices { get; init; } = [];
    public List<VideoVariantLooseCleanupDirectDeleteKeep> Keeps { get; init; } = [];
    public int Operations { get; init; }
    public int Files { get; init; }
    public long Bytes { get; init; }
    public string ApprovalHash { get; set; } = string.Empty;
}

public sealed record VideoVariantLooseCleanupDirectDeleteKeep
{
    public string Path { get; init; } = string.Empty;
    public long Size { get; init; }
    public long LastWriteUtcTicks { get; init; }
}

public sealed record VideoVariantLooseCleanupDirectDeleteReceipt
{
    public int Version { get; init; }
    public string PlanApprovalHash { get; init; } = string.Empty;
    public string SourceManifestApprovalHash { get; init; } = string.Empty;
    public string SourceOperationSetHash { get; init; } = string.Empty;
    public string SourceJournalSha256 { get; init; } = string.Empty;
    public Guid SourceJournalRunId { get; init; }
    public int SourceJournalBatchSize { get; init; }
    public int SourceJournalBatchCount { get; init; }
    public Guid RunId { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset CompletedUtc { get; init; }
    public int Operations { get; init; }
    public int FilesDeleted { get; init; }
    public long BytesDeleted { get; init; }
    public int RowsDeleted { get; init; }
    public int Batches { get; init; }
    public string ReceiptHash { get; set; } = string.Empty;
}

internal sealed class VideoVariantLooseCleanupDirectDeleteJournal
{
    public int Version { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string PlanApprovalHash { get; init; } = string.Empty;
    public Guid RunId { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public int BatchSize { get; init; }
    public List<VideoVariantLooseCleanupDirectDeleteJournalBatch> Batches { get; init; } = [];
    public string JournalHash { get; set; } = string.Empty;
}

internal sealed class VideoVariantLooseCleanupDirectDeleteJournalBatch
{
    public int Index { get; init; }
    public string State { get; set; } = "pending";
    public int FilesDeleted { get; set; }
    public long BytesDeleted { get; set; }
    public int RowsDeleted { get; set; }
}
