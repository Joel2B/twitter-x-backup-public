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
        (VideoVariantLooseCleanupReceipt receipt, List<PurgeWork> work, string root) =
            ValidateAndBuild(manifest, receiptPath, drive);
        return new(
            drive.ToUpperInvariant(),
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
        (VideoVariantLooseCleanupReceipt applyReceipt, List<PurgeWork> work, string root) =
            ValidateAndBuild(manifest, applyReceiptPath, drive);
        if (!approval.Equals(applyReceipt.ReceiptHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Approval hash does not match apply receipt.");

        string fullJournalPath = RequireLocalOutputPath(journalPath, nameof(journalPath));
        string fullReceiptPath = RequireLocalOutputPath(receiptPath, nameof(receiptPath));
        if (File.Exists(fullReceiptPath))
        {
            VideoVariantLooseCleanupPurgeReceipt completed = LoadPurgeReceipt(fullReceiptPath);
            if (
                completed.ApplyReceiptHash == applyReceipt.ReceiptHash
                && completed.Drive.Equals(drive, StringComparison.OrdinalIgnoreCase)
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
            drive,
            fullJournalPath,
            batchSize,
            batchCount,
            resume
        );
        DateTimeOffset startedUtc = journal.StartedUtc;
        string mediaRoot = Path.GetDirectoryName(Path.GetDirectoryName(root)!)!;
        if (
            drive.Equals(
                Path.GetPathRoot(manifest.DirectRoot)?.TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase
            )
        )
            mediaRoot = manifest.DirectRoot;
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
            Drive = drive.ToUpperInvariant(),
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

    private static (
        VideoVariantLooseCleanupReceipt Receipt,
        List<PurgeWork> Work,
        string Root
    ) ValidateAndBuild(
        VideoVariantLooseCleanupApplyManifest manifest,
        string receiptPath,
        string drive
    )
    {
        if (drive.Length != 1 || !char.IsLetter(drive[0]))
            throw new ArgumentException("Drive must be one letter.", nameof(drive));
        drive = drive.ToUpperInvariant();
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
        string rootPrefix = drive + ":\\";
        string root =
            receipt.QuarantineRoots.SingleOrDefault(value =>
                value.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            )
            ?? throw new InvalidOperationException(
                $"Apply receipt has no quarantine root for {drive}:."
            );

        List<PurgeWork> work = [];

        foreach (VideoVariantLooseCleanupApplyOperation operation in manifest.Operations)
        {
            string logical = manifest.Paths[operation.RemovePathIndex];
            string originalRoot = config.MediaRoots[operation.PartitionId];
            if (
                Path.GetPathRoot(originalRoot)
                    ?.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                is true
            )
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
                && Path.GetPathRoot(manifest.DirectRoot)
                    ?.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                    is true
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
            throw new InvalidOperationException($"Manifest has no purge files for {drive}:.");
        if (work.Any(value => !IsWithinRoot(value.Path, root)))
            throw new InvalidOperationException(
                "Derived purge path escapes receipt quarantine root."
            );
        return (receipt, work.OrderBy(value => value.Path, StringComparer.Ordinal).ToList(), root);
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
                || !existing.Drive.Equals(drive, StringComparison.OrdinalIgnoreCase)
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
            Drive = drive.ToUpperInvariant(),
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

    private static bool IsWithinRoot(string path, string root)
    {
        string prefix =
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
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
    public int Batches { get; init; }
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
