using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Backup.Infrastructure.Media.Maintenance;

public sealed class VideoVariantLooseCleanupPurgeService
{
    private const string QuarantineDirectory = ".video-variant-loose-quarantine";
    private const string LockFileName = ".video-variant-loose-cleanup.lock";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    internal Action<string>? FaultInjection { get; init; }

    public VideoVariantLooseCleanupPurgePlan Plan(
        VideoVariantLooseCleanupApplyManifest manifest,
        string receiptPath,
        string drive
    )
    {
        (VideoVariantLooseCleanupReceipt receipt, List<PurgeWork> work, string root, _) =
            ValidateAndBuild(manifest, receiptPath, drive);
        return new(
            NormalizeSelector(drive),
            root,
            work.Count,
            work.Sum(value => value.Size),
            receipt.ReceiptHash
        );
    }

    public async Task<VideoVariantLooseCleanupPurgeReceipt> PurgeAsync(
        VideoVariantLooseCleanupApplyManifest manifest,
        string applyReceiptPath,
        string drive,
        string approval,
        string journalPath,
        string receiptPath,
        int batchSize = 1000,
        bool resume = false,
        bool runtimeStoppedConfirmed = false,
        IProgress<VideoVariantLooseCleanupPurgeProgress>? progress = null,
        CancellationToken token = default
    )
    {
        if (!runtimeStoppedConfirmed)
            throw new InvalidOperationException("Runtime stopped confirmation is required.");
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        (
            VideoVariantLooseCleanupReceipt applyReceipt,
            List<PurgeWork> work,
            string root,
            string mediaRoot
        ) = ValidateAndBuild(manifest, applyReceiptPath, drive);
        string selector = NormalizeSelector(drive);
        if (!approval.Equals(applyReceipt.ReceiptHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Approval hash does not match apply receipt.");

        string fullJournalPath = RequireLocalOutputPath(journalPath, nameof(journalPath));
        string fullReceiptPath = RequireLocalOutputPath(receiptPath, nameof(receiptPath));
        if (File.Exists(fullReceiptPath))
        {
            VideoVariantLooseCleanupPurgeReceipt completed = LoadPurgeReceipt(fullReceiptPath);
            if (
                completed.ApplyReceiptHash == applyReceipt.ReceiptHash
                && PhysicalPathSemantics.Comparer.Equals(completed.Drive, selector)
            )
                return completed;
            throw new InvalidOperationException(
                "Purge receipt path already contains a different receipt."
            );
        }

        int batchCount = (work.Count + batchSize - 1) / batchSize;
        VideoVariantLooseCleanupPurgeJournal journal = LoadOrCreateJournal(
            manifest,
            applyReceipt,
            selector,
            fullJournalPath,
            batchSize,
            batchCount,
            resume
        );
        DateTimeOffset startedUtc = journal.StartedUtc;
        await using FileStream fileLock = new(
            Path.Combine(mediaRoot, LockFileName),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );

        for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            VideoVariantLooseCleanupPurgeJournalBatch state = journal.Batches[batchIndex];
            if (state.State == "verified")
                continue;
            token.ThrowIfCancellationRequested();
            PurgeWork[] batch = work.Skip(batchIndex * batchSize).Take(batchSize).ToArray();
            bool allowAbsent = resume && state.State == "deleting";
            state.State = "deleting";
            WriteJsonAtomic(fullJournalPath, journal);

            foreach (PurgeWork item in batch)
            {
                token.ThrowIfCancellationRequested();
                FileInfo info = new(item.Path);
                if (!info.Exists)
                {
                    if (!allowAbsent)
                        throw new InvalidOperationException(
                            $"Quarantine file is absent before purge: {item.Path}"
                        );
                    continue;
                }
                if (
                    info.Length != item.Size
                    || info.LastWriteTimeUtc.Ticks != item.LastWriteUtcTicks
                )
                    throw new InvalidOperationException(
                        $"Quarantine file changed before purge: {item.Path}"
                    );
                info.Delete();
                FaultInjection?.Invoke("after-delete");
            }
            if (batch.Any(item => File.Exists(item.Path)))
                throw new InvalidOperationException(
                    $"Purge batch {batchIndex} still contains files."
                );
            state.State = "verified";
            state.FilesDeleted = batch.Length;
            state.BytesDeleted = batch.Sum(value => value.Size);
            WriteJsonAtomic(fullJournalPath, journal);
            progress?.Report(
                new(
                    batchIndex + 1,
                    batchCount,
                    journal.Batches.Sum(value => value.FilesDeleted),
                    work.Count,
                    journal.Batches.Sum(value => value.BytesDeleted),
                    work.Sum(value => value.Size)
                )
            );
        }

        VideoVariantLooseCleanupPurgeReceipt purgeReceipt = new()
        {
            Version = 1,
            ApplyReceiptHash = applyReceipt.ReceiptHash,
            OperationSetHash = manifest.OperationSetHash,
            Drive = selector,
            QuarantineRoot = root,
            Host = Environment.MachineName,
            ProcessId = Environment.ProcessId,
            RunId = journal.RunId,
            StartedUtc = startedUtc,
            CompletedUtc = DateTimeOffset.UtcNow,
            FilesDeleted = journal.Batches.Sum(value => value.FilesDeleted),
            BytesDeleted = journal.Batches.Sum(value => value.BytesDeleted),
            Batches = batchCount,
        };
        purgeReceipt.ReceiptHash = CalculatePurgeReceiptHash(purgeReceipt);
        WriteJsonAtomic(fullReceiptPath, purgeReceipt);
        File.Delete(fullJournalPath);
        return purgeReceipt;
    }

    public VideoVariantLooseCleanupPurgeReceipt ReconcileEmpty(
        VideoVariantLooseCleanupApplyManifest manifest,
        string applyReceiptPath,
        string drive,
        string approval,
        string journalPath,
        string receiptPath,
        bool runtimeStoppedConfirmed = false
    )
    {
        if (!runtimeStoppedConfirmed)
            throw new InvalidOperationException("Runtime stopped confirmation is required.");
        (
            VideoVariantLooseCleanupReceipt applyReceipt,
            List<PurgeWork> work,
            string root,
            string mediaRoot
        ) = ValidateAndBuild(manifest, applyReceiptPath, drive);
        string selector = NormalizeSelector(drive);
        if (!approval.Equals(applyReceipt.ReceiptHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Approval hash does not match apply receipt.");

        string fullJournalPath = RequireLocalOutputPath(journalPath, nameof(journalPath));
        string fullReceiptPath = RequireLocalOutputPath(receiptPath, nameof(receiptPath));
        if (!File.Exists(fullJournalPath))
            throw new InvalidOperationException("Purge journal does not exist.");

        byte[] journalBytes = File.ReadAllBytes(fullJournalPath);
        VideoVariantLooseCleanupPurgeJournal parsedJournal =
            JsonSerializer.Deserialize<VideoVariantLooseCleanupPurgeJournal>(
                journalBytes,
                JsonOptions
            ) ?? throw new InvalidOperationException("Invalid purge journal.");
        VideoVariantLooseCleanupPurgeJournal journal = LoadOrCreateJournal(
            manifest,
            applyReceipt,
            selector,
            fullJournalPath,
            parsedJournal.BatchSize,
            parsedJournal.Batches.Count,
            resume: true
        );
        for (int batchIndex = 0; batchIndex < journal.Batches.Count; batchIndex++)
        {
            VideoVariantLooseCleanupPurgeJournalBatch batch = journal.Batches[batchIndex];
            PurgeWork[] batchWork = work
                .Skip(batchIndex * journal.BatchSize)
                .Take(journal.BatchSize)
                .ToArray();
            bool verified = batch.State == "verified";
            if (
                batch.Index != batchIndex
                || batch.State is not ("pending" or "deleting" or "verified")
                || batch.FilesDeleted != (verified ? batchWork.Length : 0)
                || batch.BytesDeleted != (verified ? batchWork.Sum(value => value.Size) : 0)
            )
                throw new InvalidOperationException("Purge journal batch state is invalid.");
        }

        using FileStream fileLock = new(
            Path.Combine(mediaRoot, LockFileName),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );
        if (Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any())
            throw new InvalidOperationException("Quarantine root is not empty; reconciliation refused.");

        int filesDeleted = journal.Batches.Sum(batch => batch.FilesDeleted);
        long bytesDeleted = journal.Batches.Sum(batch => batch.BytesDeleted);
        if (filesDeleted < 0 || filesDeleted > work.Count || bytesDeleted < 0 || bytesDeleted > work.Sum(value => value.Size))
            throw new InvalidOperationException("Purge journal counters are invalid.");

        string journalSha256 = Sha256(journalBytes);
        if (File.Exists(fullReceiptPath))
        {
            VideoVariantLooseCleanupPurgeReceipt completed = LoadPurgeReceipt(fullReceiptPath);
            if (
                completed.Version != 2
                || completed.ApplyReceiptHash != applyReceipt.ReceiptHash
                || !PhysicalPathSemantics.Comparer.Equals(completed.Drive, selector)
                || completed.ReconciledJournalSha256 != journalSha256
            )
                throw new InvalidOperationException(
                    "Purge receipt path already contains a different receipt."
                );
            PreserveReconciledJournal(fullJournalPath, journalBytes);
            File.Delete(fullJournalPath);
            return completed;
        }

        VideoVariantLooseCleanupPurgeReceipt purgeReceipt = new()
        {
            Version = 2,
            ApplyReceiptHash = applyReceipt.ReceiptHash,
            OperationSetHash = manifest.OperationSetHash,
            Drive = selector,
            QuarantineRoot = root,
            Host = Environment.MachineName,
            ProcessId = Environment.ProcessId,
            RunId = journal.RunId,
            StartedUtc = journal.StartedUtc,
            CompletedUtc = DateTimeOffset.UtcNow,
            FilesDeleted = filesDeleted,
            BytesDeleted = bytesDeleted,
            FilesAlreadyAbsent = work.Count - filesDeleted,
            BytesAlreadyAbsent = work.Sum(value => value.Size) - bytesDeleted,
            Batches = journal.Batches.Count,
            ReconciledJournalSha256 = journalSha256,
        };
        purgeReceipt.ReceiptHash = CalculatePurgeReceiptHash(purgeReceipt);
        PreserveReconciledJournal(fullJournalPath, journalBytes);
        WriteJsonAtomic(fullReceiptPath, purgeReceipt);
        File.Delete(fullJournalPath);
        return purgeReceipt;
    }

    private static void PreserveReconciledJournal(string journalPath, byte[] journalBytes)
    {
        string reconciledJournalPath = journalPath + ".reconciled";
        if (File.Exists(reconciledJournalPath))
        {
            if (Sha256(File.ReadAllBytes(reconciledJournalPath)) != Sha256(journalBytes))
                throw new InvalidOperationException("Reconciled purge journal changed.");
            return;
        }
        File.Copy(journalPath, reconciledJournalPath);
    }

    private static (
        VideoVariantLooseCleanupReceipt Receipt,
        List<PurgeWork> Work,
        string Root,
        string MediaRoot
    ) ValidateAndBuild(
        VideoVariantLooseCleanupApplyManifest manifest,
        string receiptPath,
        string drive
    )
    {
        string selector = NormalizeSelector(drive);
        if (
            VideoVariantLooseCleanupApplyService.CalculateOperationSetHash(manifest)
                != manifest.OperationSetHash
            || VideoVariantLooseCleanupApplyService.CalculateApprovalHash(manifest)
                != manifest.ApprovalHash
        )
            throw new InvalidOperationException("Apply manifest hash is invalid.");
        FileInfo configInfo = new(manifest.DataConfigPath);
        if (
            !configInfo.Exists
            || configInfo.Length != manifest.DataConfigLength
            || configInfo.LastWriteTimeUtc.Ticks != manifest.DataConfigLastWriteUtcTicks
            || Sha256(File.ReadAllBytes(configInfo.FullName)) != manifest.DataConfigSha256
        )
            throw new InvalidOperationException("Data config fingerprint changed.");
        ResolvedConfig config = VideoVariantLooseCleanupPreviewService.LoadConfig(
            manifest.DataConfigPath,
            manifest.MediaStoreId,
            manifest.CacheId
        );
        VideoVariantLooseCleanupReceipt receipt = LoadApplyReceipt(receiptPath);
        if (
            receipt.ApprovalHash != manifest.ApprovalHash
            || receipt.OperationSetHash != manifest.OperationSetHash
        )
            throw new InvalidOperationException("Receipt does not match manifest.");
        string root = SelectQuarantineRoot(
            selector,
            receipt.QuarantineRoots,
            OperatingSystem.IsWindows()
        );
        string[] mediaRoots = config
            .MediaRoots.Values.Append(manifest.DirectRoot)
            .Distinct(PhysicalPathSemantics.Comparer)
            .Where(candidate =>
                PhysicalPathSemantics.Comparer.Equals(
                    Path.Combine(candidate, QuarantineDirectory, manifest.ApprovalHash),
                    root
                )
            )
            .ToArray();
        if (mediaRoots.Length != 1)
            throw new InvalidOperationException(
                "Selected quarantine root does not map to exactly one media root."
            );
        string mediaRoot = mediaRoots[0];

        List<PurgeWork> work = [];

        IReadOnlyList<int> operationIndices =
            VideoVariantLooseCleanupApplyService.ValidateReceiptOperationIndices(
                receipt,
                manifest.Operations.Count
            );

        foreach (int operationIndex in operationIndices)
        {
            VideoVariantLooseCleanupApplyOperation operation = manifest.Operations[operationIndex];
            string logical = manifest.Paths[operation.RemovePathIndex];
            string originalRoot = config.MediaRoots[operation.PartitionId];
            if (PhysicalPathSemantics.Comparer.Equals(originalRoot, mediaRoot))
            {
                string hash = Sha256(
                    Encoding.UTF8.GetBytes($"original\n{operation.PartitionId}\n{logical}")
                );
                work.Add(
                    new(
                        Path.Combine(
                            originalRoot,
                            QuarantineDirectory,
                            manifest.ApprovalHash,
                            "original",
                            operation.PartitionId.ToString(),
                            hash[..2],
                            hash + ".bin"
                        ),
                        operation.OriginalSize,
                        operation.OriginalLastWriteUtcTicks
                    )
                );
            }
            if (
                operation.DirectPresent
                && PhysicalPathSemantics.Comparer.Equals(manifest.DirectRoot, mediaRoot)
            )
            {
                string hash = Sha256(
                    Encoding.UTF8.GetBytes($"direct\n{operation.PartitionId}\n{logical}")
                );
                work.Add(
                    new(
                        Path.Combine(
                            manifest.DirectRoot,
                            QuarantineDirectory,
                            manifest.ApprovalHash,
                            "direct",
                            hash[..2],
                            hash + ".bin"
                        ),
                        operation.DirectSize,
                        operation.DirectLastWriteUtcTicks
                    )
                );
            }
        }
        if (work.Count == 0)
            throw new InvalidOperationException($"Manifest has no purge files for {selector}.");
        if (work.Any(value => !IsWithinRoot(value.Path, root)))
            throw new InvalidOperationException(
                "Derived purge path escapes receipt quarantine root."
            );
        return (
            receipt,
            work.OrderBy(value => value.Path, PhysicalPathSemantics.Comparer).ToList(),
            root,
            mediaRoot
        );
    }

    internal static string SelectQuarantineRoot(
        string selector,
        IReadOnlyList<string> quarantineRoots,
        bool windows
    )
    {
        string normalizedSelector = NormalizeSelector(selector, windows);
        StringComparer comparer = PhysicalPathSemantics.ComparerFor(windows);
        string[] matches = quarantineRoots
            .Where(root =>
                windows && normalizedSelector.Length == 1
                    ? root.Length >= 3
                        && char.ToUpperInvariant(root[0]) == normalizedSelector[0]
                        && root[1] == ':'
                        && (root[2] == '\\' || root[2] == '/')
                    : IsPortableWithin(root, normalizedSelector, windows)
            )
            .Distinct(comparer)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Purge selector must resolve exactly one receipt quarantine root."
            );
        return matches[0];
    }

    internal static string NormalizeSelector(string selector, bool? windows = null)
    {
        bool useWindows = windows ?? OperatingSystem.IsWindows();
        if (useWindows && selector.Length == 1 && char.IsLetter(selector[0]))
            return selector.ToUpperInvariant();
        if (useWindows || selector.Length == 0 || selector[0] != '/')
            throw new ArgumentException(
                "Drive must be one Windows drive letter or an absolute Linux path.",
                nameof(selector)
            );
        return selector.Length == 1 ? selector : selector.TrimEnd('/');
    }

    private static bool IsPortableWithin(string path, string root, bool windows)
    {
        char separator = windows ? '\\' : '/';
        string normalizedPath = path.Replace(windows ? '/' : '\\', separator).TrimEnd(separator);
        string normalizedRoot = root.Replace(windows ? '/' : '\\', separator).TrimEnd(separator);
        StringComparison comparison = PhysicalPathSemantics.ComparisonFor(windows);
        return normalizedPath.Equals(normalizedRoot, comparison)
            || normalizedPath.StartsWith(normalizedRoot + separator, comparison);
    }

    private static VideoVariantLooseCleanupPurgeJournal LoadOrCreateJournal(
        VideoVariantLooseCleanupApplyManifest manifest,
        VideoVariantLooseCleanupReceipt receipt,
        string drive,
        string path,
        int batchSize,
        int batchCount,
        bool resume
    )
    {
        if (File.Exists(path))
        {
            if (!resume)
                throw new InvalidOperationException("Existing purge journal requires --resume.");
            VideoVariantLooseCleanupPurgeJournal existing =
                JsonSerializer.Deserialize<VideoVariantLooseCleanupPurgeJournal>(
                    File.ReadAllBytes(path),
                    JsonOptions
                ) ?? throw new InvalidOperationException("Invalid purge journal.");
            if (
                existing.ApplyReceiptHash != receipt.ReceiptHash
                || existing.OperationSetHash != manifest.OperationSetHash
                || !PhysicalPathSemantics.Comparer.Equals(existing.Drive, drive)
                || existing.BatchSize != batchSize
                || existing.Batches.Count != batchCount
            )
                throw new InvalidOperationException("Purge journal does not match operation.");
            return existing;
        }
        if (resume)
            throw new InvalidOperationException(
                "Resume requested but purge journal does not exist."
            );
        VideoVariantLooseCleanupPurgeJournal journal = new()
        {
            ApplyReceiptHash = receipt.ReceiptHash,
            OperationSetHash = manifest.OperationSetHash,
            Drive = drive,
            RunId = Guid.NewGuid(),
            StartedUtc = DateTimeOffset.UtcNow,
            BatchSize = batchSize,
            Batches = Enumerable
                .Range(0, batchCount)
                .Select(index => new VideoVariantLooseCleanupPurgeJournalBatch { Index = index })
                .ToList(),
        };
        WriteJsonAtomic(path, journal);
        return journal;
    }

    private static VideoVariantLooseCleanupReceipt LoadApplyReceipt(string path)
    {
        VideoVariantLooseCleanupReceipt receipt =
            JsonSerializer.Deserialize<VideoVariantLooseCleanupReceipt>(
                File.ReadAllBytes(path),
                JsonOptions
            ) ?? throw new InvalidOperationException("Invalid apply receipt.");
        if (
            VideoVariantLooseCleanupApplyService.CalculateReceiptHash(receipt)
            != receipt.ReceiptHash
        )
            throw new InvalidOperationException("Apply receipt hash is invalid.");
        return receipt;
    }

    private static VideoVariantLooseCleanupPurgeReceipt LoadPurgeReceipt(string path)
    {
        VideoVariantLooseCleanupPurgeReceipt receipt =
            JsonSerializer.Deserialize<VideoVariantLooseCleanupPurgeReceipt>(
                File.ReadAllBytes(path),
                JsonOptions
            ) ?? throw new InvalidOperationException("Invalid purge receipt.");
        if (CalculatePurgeReceiptHash(receipt) != receipt.ReceiptHash)
            throw new InvalidOperationException("Purge receipt hash is invalid.");
        return receipt;
    }

    internal static string CalculatePurgeReceiptHash(
        VideoVariantLooseCleanupPurgeReceipt receipt
    ) => receipt.Version == 1
        ? Sha256(
            JsonSerializer.SerializeToUtf8Bytes(
                new VideoVariantLooseCleanupPurgeReceiptV1
                {
                    Version = receipt.Version,
                    ApplyReceiptHash = receipt.ApplyReceiptHash,
                    OperationSetHash = receipt.OperationSetHash,
                    Drive = receipt.Drive,
                    QuarantineRoot = receipt.QuarantineRoot,
                    Host = receipt.Host,
                    ProcessId = receipt.ProcessId,
                    RunId = receipt.RunId,
                    StartedUtc = receipt.StartedUtc,
                    CompletedUtc = receipt.CompletedUtc,
                    FilesDeleted = receipt.FilesDeleted,
                    BytesDeleted = receipt.BytesDeleted,
                    Batches = receipt.Batches,
                    ReceiptHash = string.Empty,
                },
                JsonOptions
            )
        )
        : Sha256(
            JsonSerializer.SerializeToUtf8Bytes(
                receipt with { ReceiptHash = string.Empty },
                JsonOptions
            )
        );

    private static bool IsWithinRoot(string path, string root)
    {
        string prefix =
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(prefix, PhysicalPathSemantics.Comparison);
    }

    private static string RequireLocalOutputPath(string path, string name)
    {
        string full = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(full);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"{name} directory does not exist: {directory}");
        return full;
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void WriteJsonAtomic<T>(string path, T value)
    {
        string temporary = path + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));
        File.Move(temporary, path, overwrite: true);
    }

    private sealed record PurgeWork(string Path, long Size, long LastWriteUtcTicks);

    private sealed record VideoVariantLooseCleanupPurgeReceiptV1
    {
        public int Version { get; init; }
        public string ApplyReceiptHash { get; init; } = string.Empty;
        public string OperationSetHash { get; init; } = string.Empty;
        public string Drive { get; init; } = string.Empty;
        public string QuarantineRoot { get; init; } = string.Empty;
        public string Host { get; init; } = string.Empty;
        public int ProcessId { get; init; }
        public Guid RunId { get; init; }
        public DateTimeOffset StartedUtc { get; init; }
        public DateTimeOffset CompletedUtc { get; init; }
        public int FilesDeleted { get; init; }
        public long BytesDeleted { get; init; }
        public int Batches { get; init; }
        public string ReceiptHash { get; init; } = string.Empty;
    }
}

public sealed record VideoVariantLooseCleanupPurgePlan(
    string Drive,
    string QuarantineRoot,
    int Files,
    long Bytes,
    string ApplyReceiptHash
);

public sealed record VideoVariantLooseCleanupPurgeProgress(
    int CompletedBatches,
    int TotalBatches,
    int FilesDeleted,
    int TotalFiles,
    long BytesDeleted,
    long TotalBytes
);

public sealed record VideoVariantLooseCleanupPurgeReceipt
{
    public int Version { get; init; }
    public string ApplyReceiptHash { get; init; } = string.Empty;
    public string OperationSetHash { get; init; } = string.Empty;
    public string Drive { get; init; } = string.Empty;
    public string QuarantineRoot { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public Guid RunId { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset CompletedUtc { get; init; }
    public int FilesDeleted { get; init; }
    public long BytesDeleted { get; init; }
    public int FilesAlreadyAbsent { get; init; }
    public long BytesAlreadyAbsent { get; init; }
    public int Batches { get; init; }
    public string ReconciledJournalSha256 { get; init; } = string.Empty;
    public string ReceiptHash { get; set; } = string.Empty;
}

internal sealed class VideoVariantLooseCleanupPurgeJournal
{
    public string ApplyReceiptHash { get; init; } = string.Empty;
    public string OperationSetHash { get; init; } = string.Empty;
    public string Drive { get; init; } = string.Empty;
    public Guid RunId { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public int BatchSize { get; init; }
    public List<VideoVariantLooseCleanupPurgeJournalBatch> Batches { get; init; } = [];
}

internal sealed class VideoVariantLooseCleanupPurgeJournalBatch
{
    public int Index { get; init; }
    public string State { get; set; } = "pending";
    public int FilesDeleted { get; set; }
    public long BytesDeleted { get; set; }
}
