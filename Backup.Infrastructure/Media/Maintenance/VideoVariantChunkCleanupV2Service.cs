using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.Maintenance;

public sealed class VideoVariantChunkCleanupV2Service
{
    private const int CopyBufferSize = 2 * 1024 * 1024;

    private static readonly Regex VideoPathPattern = new(
        @"^(?<post>[^/]+)/video/(?<media>[^/]+)/mp4/(?<video>[^/]+)/(?<width>[1-9]\d*)x(?<height>[1-9]\d*)\.mp4$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<VideoVariantChunkCleanupV2Manifest> PreviewAsync(
        string backupRoot,
        int parallelism = 8,
        IProgress<VideoVariantChunkCleanupV2PreviewProgress>? progress = null,
        CancellationToken token = default
    )
    {
        ValidateParallelism(parallelism);
        string fullRoot = Path.GetFullPath(backupRoot);
        string chunksRoot = Path.Combine(fullRoot, "chunks");
        string[] manifestPaths = Directory.Exists(chunksRoot)
            ? Directory
                .GetFiles(chunksRoot, "*.chunk.json", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .ToArray()
            : [];
        ConcurrentBag<PreviewChunk> chunks = [];
        ConcurrentBag<string> errors = [];
        int completed = 0;
        long entries = 0;
        long videos = 0;
        Stopwatch timer = Stopwatch.StartNew();

        await Parallel.ForEachAsync(
            manifestPaths,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = token },
            async (manifestPath, cancellationToken) =>
            {
                try
                {
                    byte[] json = await File.ReadAllBytesAsync(manifestPath, cancellationToken);
                    Chunk chunk =
                        JsonSerializer.Deserialize<Chunk>(json, JsonOptions)
                        ?? throw new InvalidOperationException("Chunk JSON is null.");
                    string zipPath = Path.Combine(chunksRoot, $"{chunk.Id}.chunk.zip");

                    if (!File.Exists(zipPath))
                        throw new FileNotFoundException("Chunk ZIP not found.", zipPath);

                    FileInfo zipInfo = new(zipPath);
                    FileInfo jsonInfo = new(manifestPath);
                    List<PreviewVideo> chunkVideos = [];

                    foreach (ChunkData data in chunk.Data)
                    {
                        string path = NormalizePath(data.Path);
                        Match match = VideoPathPattern.Match(path);

                        if (!match.Success)
                            continue;

                        chunkVideos.Add(
                            new PreviewVideo(
                                chunk.Id,
                                path,
                                $"{match.Groups["post"].Value}/{match.Groups["media"].Value}",
                                int.Parse(match.Groups["width"].Value),
                                int.Parse(match.Groups["height"].Value)
                            )
                        );
                    }

                    chunks.Add(
                        new PreviewChunk(
                            chunk.Id,
                            zipInfo.Length,
                            zipInfo.LastWriteTimeUtc.Ticks,
                            jsonInfo.Length,
                            jsonInfo.LastWriteTimeUtc.Ticks,
                            chunkVideos
                        )
                    );
                    Interlocked.Add(ref entries, chunk.Data.Count);
                    Interlocked.Add(ref videos, chunkVideos.Count);
                }
                catch (Exception ex)
                    when (ex
                            is IOException
                                or UnauthorizedAccessException
                                or JsonException
                                or InvalidOperationException
                    )
                {
                    errors.Add($"{manifestPath}: {ex.Message}");
                }
                finally
                {
                    int done = Interlocked.Increment(ref completed);
                    progress?.Report(
                        new VideoVariantChunkCleanupV2PreviewProgress
                        {
                            CompletedChunks = done,
                            TotalChunks = manifestPaths.Length,
                            Entries = Interlocked.Read(ref entries),
                            VideoEntries = Interlocked.Read(ref videos),
                            Elapsed = timer.Elapsed,
                            Parallelism = parallelism,
                        }
                    );
                }
            }
        );

        if (!errors.IsEmpty)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Order()));

        List<PreviewChunk> orderedChunks = chunks.OrderBy(chunk => chunk.Id).ToList();
        List<PreviewVideo> allVideos = orderedChunks.SelectMany(chunk => chunk.Videos).ToList();
        List<(string Remove, string Keep)> variants = [];

        foreach (
            IGrouping<string, PreviewVideo> group in allVideos.GroupBy(
                video => video.Group,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            List<PreviewVideo> distinct = group
                .GroupBy(video => video.Path, StringComparer.Ordinal)
                .Select(items => items.First())
                .OrderByDescending(video => (long)video.Width * video.Height)
                .ThenByDescending(video => video.Width)
                .ThenByDescending(video => video.Height)
                .ThenBy(video => video.Path, StringComparer.Ordinal)
                .ToList();

            if (distinct.Count < 2)
                continue;

            PreviewVideo keep = distinct[0];
            long keepArea = (long)keep.Width * keep.Height;

            foreach (PreviewVideo remove in distinct.Skip(1))
            {
                long removeArea = (long)remove.Width * remove.Height;

                if (
                    removeArea < keepArea
                    && remove.Width <= keep.Width
                    && remove.Height <= keep.Height
                )
                {
                    variants.Add((remove.Path, keep.Path));
                }
            }
        }

        variants = variants
            .Distinct()
            .OrderBy(item => item.Remove, StringComparer.Ordinal)
            .ThenBy(item => item.Keep, StringComparer.Ordinal)
            .ToList();
        string[] paths = variants
            .SelectMany(item => new[] { item.Remove, item.Keep })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, int> pathIndices = paths
            .Select((path, index) => (path, index))
            .ToDictionary(item => item.path, item => item.index, StringComparer.Ordinal);
        HashSet<string> removalPaths = variants
            .Select(item => item.Remove)
            .ToHashSet(StringComparer.Ordinal);
        VideoVariantChunkCleanupV2Manifest manifest = new()
        {
            Version = 2,
            CreatedUtc = DateTimeOffset.UtcNow,
            BackupRoot = fullRoot,
            Paths = [.. paths],
            Variants = variants
                .Select(item => new VideoVariantChunkCleanupV2Variant
                {
                    RemovePathIndex = pathIndices[item.Remove],
                    KeepPathIndex = pathIndices[item.Keep],
                })
                .ToList(),
            Chunks = orderedChunks
                .Select(chunk => new VideoVariantChunkCleanupV2Chunk
                {
                    Id = chunk.Id,
                    ZipLength = chunk.ZipLength,
                    ZipLastWriteUtcTicks = chunk.ZipLastWriteUtcTicks,
                    ManifestLength = chunk.ManifestLength,
                    ManifestLastWriteUtcTicks = chunk.ManifestLastWriteUtcTicks,
                    RemovePathIndices = chunk
                        .Videos.Select(video => video.Path)
                        .Where(removalPaths.Contains)
                        .Distinct(StringComparer.Ordinal)
                        .Select(path => pathIndices[path])
                        .Order()
                        .ToList(),
                })
                .ToList(),
        };
        manifest.ApprovalHash = CalculateApprovalHash(manifest);
        return manifest;
    }

    public void SaveManifest(VideoVariantChunkCleanupV2Manifest manifest, string outputPath)
    {
        ValidateManifest(manifest, verifyHash: true);
        WriteJsonAtomic(outputPath, manifest);
    }

    public VideoVariantChunkCleanupV2Manifest LoadManifest(string manifestPath)
    {
        VideoVariantChunkCleanupV2Manifest manifest =
            JsonSerializer.Deserialize<VideoVariantChunkCleanupV2Manifest>(
                File.ReadAllBytes(manifestPath),
                JsonOptions
            ) ?? throw new InvalidOperationException("Invalid video cleanup v2 manifest.");
        ValidateManifest(manifest, verifyHash: true);
        return manifest;
    }

    public string CalculateApprovalHash(VideoVariantChunkCleanupV2Manifest manifest)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                manifest.Version,
                manifest.CreatedUtc,
                manifest.BackupRoot,
                ApprovalHash = string.Empty,
                manifest.Paths,
                manifest.Variants,
                manifest.Chunks,
            },
            JsonOptions
        );
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    public async Task<VideoVariantChunkCleanupV2PreflightResult> PreflightAsync(
        VideoVariantChunkCleanupV2Manifest manifest,
        int parallelism = 8,
        IProgress<VideoVariantChunkCleanupV2PreflightProgress>? progress = null,
        CancellationToken token = default
    ) =>
        await PreflightAsync(
            manifest,
            parallelism,
            progress,
            committedChunkIds: null,
            skippedChunkIds: null,
            token
        );

    private async Task<VideoVariantChunkCleanupV2PreflightResult> PreflightAsync(
        VideoVariantChunkCleanupV2Manifest manifest,
        int parallelism,
        IProgress<VideoVariantChunkCleanupV2PreflightProgress>? progress,
        IReadOnlySet<int>? committedChunkIds,
        IReadOnlySet<int>? skippedChunkIds,
        CancellationToken token
    )
    {
        ValidateParallelism(parallelism);
        List<string> manifestErrors = [];

        try
        {
            ValidateManifest(manifest, verifyHash: true);
        }
        catch (Exception ex)
        {
            manifestErrors.Add(ex.Message);
        }

        if (manifestErrors.Count > 0)
            return new VideoVariantChunkCleanupV2PreflightResult { Errors = manifestErrors };

        ConcurrentBag<PreflightChunk> checkedChunks = [];
        int completed = 0;
        long entries = 0;
        long removals = 0;
        long bytes = 0;
        Stopwatch timer = Stopwatch.StartNew();
        string chunksRoot = Path.Combine(manifest.BackupRoot, "chunks");

        await Parallel.ForEachAsync(
            manifest.Chunks,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = token },
            async (plan, cancellationToken) =>
            {
                PreflightChunk result = await InspectChunkAsync(
                    chunksRoot,
                    plan,
                    manifest.Paths,
                    committed: committedChunkIds?.Contains(plan.Id) == true,
                    skipped: skippedChunkIds?.Contains(plan.Id) == true,
                    cancellationToken
                );
                checkedChunks.Add(result);
                Interlocked.Add(ref entries, result.EntryCount);
                Interlocked.Add(ref removals, result.RemovalCount);
                Interlocked.Add(ref bytes, result.RemovalBytes);
                int done = Interlocked.Increment(ref completed);
                progress?.Report(
                    new VideoVariantChunkCleanupV2PreflightProgress
                    {
                        CompletedChunks = done,
                        TotalChunks = manifest.Chunks.Count,
                        Entries = Interlocked.Read(ref entries),
                        Removals = Interlocked.Read(ref removals),
                        Bytes = Interlocked.Read(ref bytes),
                        Errors = checkedChunks.Sum(chunk => chunk.Errors.Count),
                        Elapsed = timer.Elapsed,
                        Parallelism = parallelism,
                    }
                );
            }
        );

        List<PreflightChunk> ordered = checkedChunks.OrderBy(chunk => chunk.Id).ToList();
        List<string> errors = ordered.SelectMany(chunk => chunk.Errors).ToList();
        HashSet<string> allEntries = ordered
            .SelectMany(chunk => chunk.Paths)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> removed = manifest
            .Chunks.SelectMany(chunk => chunk.RemovePathIndices)
            .Select(index => manifest.Paths[index])
            .ToHashSet(StringComparer.Ordinal);

        foreach (
            string keep in manifest.Variants.Select(item => manifest.Paths[item.KeepPathIndex])
        )
        {
            if (!allEntries.Contains(keep))
                errors.Add($"Kept path does not exist in preflight entries: {keep}");

            if (removed.Contains(keep))
                errors.Add($"Kept path is also scheduled for removal: {keep}");
        }

        return new VideoVariantChunkCleanupV2PreflightResult
        {
            Chunks = ordered.Count,
            Entries = ordered.Sum(chunk => chunk.EntryCount),
            Removals = ordered.Sum(chunk => chunk.RemovalCount),
            Bytes = ordered.Sum(chunk => chunk.RemovalBytes),
            Errors = errors,
        };
    }

    public async Task<VideoVariantChunkCleanupV2ApplyResult> ApplyAsync(
        VideoVariantChunkCleanupV2Manifest manifest,
        string approval,
        bool resume = false,
        IReadOnlySet<int>? skipChunkIds = null,
        IProgress<VideoVariantChunkCleanupV2ApplyProgress>? progress = null,
        CancellationToken token = default
    )
    {
        ValidateManifest(manifest, verifyHash: true);

        if (!approval.Equals(manifest.ApprovalHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Approval hash does not match manifest.");

        string lockPath = Path.Combine(manifest.BackupRoot, ".video-variant-cleanup.lock");
        string journalPath = Path.Combine(manifest.BackupRoot, ".video-variant-cleanup.run.json");
        Directory.CreateDirectory(manifest.BackupRoot);
        await using FileStream cleanupLock = new(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            1,
            FileOptions.Asynchronous
        );
        VideoVariantChunkCleanupV2Journal journal;
        VideoVariantChunkCleanupV2PreflightResult preflight;
        Dictionary<int, VideoVariantChunkCleanupV2Chunk> plans = manifest.Chunks.ToDictionary(
            chunk => chunk.Id
        );

        if (File.Exists(journalPath))
        {
            if (!resume)
                throw new InvalidOperationException(
                    "Cleanup journal exists; --resume is required."
                );

            journal =
                JsonSerializer.Deserialize<VideoVariantChunkCleanupV2Journal>(
                    await File.ReadAllBytesAsync(journalPath, token),
                    JsonOptions
                ) ?? throw new InvalidOperationException("Cleanup journal is invalid.");

            if (
                !journal.ApprovalHash.Equals(
                    manifest.ApprovalHash,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                throw new InvalidOperationException(
                    "Cleanup journal approval hash does not match manifest."
                );

            await RecoverAsync(manifest, journal, plans, journalPath, token);
            MarkSkippedChunks(journal, skipChunkIds);
            WriteJsonAtomic(journalPath, journal);
            HashSet<int> committedChunkIds = journal
                .Chunks.Where(chunk => chunk.State == "committed")
                .Select(chunk => chunk.Id)
                .ToHashSet();
            HashSet<int> skippedChunkIds = journal
                .Chunks.Where(chunk => chunk.State == "skipped")
                .Select(chunk => chunk.Id)
                .ToHashSet();
            preflight = await PreflightAsync(
                manifest,
                parallelism: 8,
                progress: null,
                committedChunkIds,
                skippedChunkIds,
                token
            );
        }
        else
        {
            preflight = await PreflightAsync(
                manifest,
                parallelism: 8,
                progress: null,
                committedChunkIds: null,
                skippedChunkIds: skipChunkIds,
                token
            );
            ThrowIfPreflightFailed(preflight);
            journal = new VideoVariantChunkCleanupV2Journal
            {
                ApprovalHash = manifest.ApprovalHash,
                RunId = Guid.NewGuid().ToString("N"),
                Chunks = manifest
                    .Chunks.Where(chunk => chunk.RemovePathIndices.Count > 0)
                    .Select(chunk => new VideoVariantChunkCleanupV2JournalChunk { Id = chunk.Id })
                    .ToList(),
            };
            MarkSkippedChunks(journal, skipChunkIds);
            WriteJsonAtomic(journalPath, journal);
        }

        ThrowIfPreflightFailed(preflight);
        int total = journal.Chunks.Count(chunk => chunk.State != "skipped");
        int committed = journal.Chunks.Count(chunk => chunk.State == "committed");
        long removedBytes = 0;

        foreach (VideoVariantChunkCleanupV2JournalChunk state in journal.Chunks)
        {
            if (state.State is "committed" or "skipped")
                continue;

            token.ThrowIfCancellationRequested();
            VideoVariantChunkCleanupV2Chunk plan = plans[state.Id];

            try
            {
                long chunkBytes = await RewriteChunkAsync(manifest, plan, journal.RunId, token);
                state.State = "committed";
                WriteJsonAtomic(journalPath, journal);
                CleanupCommittedArtifacts(manifest.BackupRoot, plan.Id, journal.RunId);
                removedBytes += chunkBytes;
                committed++;
                progress?.Report(
                    new VideoVariantChunkCleanupV2ApplyProgress
                    {
                        CommittedChunks = committed,
                        TotalChunks = total,
                        CurrentChunkId = state.Id,
                        RemovedEntries = plan.RemovePathIndices.Count,
                        RemovedBytes = chunkBytes,
                    }
                );
            }
            catch
            {
                RestoreArtifacts(manifest.BackupRoot, plan.Id, journal.RunId);
                throw;
            }
        }

        File.Delete(journalPath);
        return new VideoVariantChunkCleanupV2ApplyResult
        {
            Chunks = total,
            Removals = preflight.Removals,
            Bytes = preflight.Bytes == 0 ? removedBytes : preflight.Bytes,
            SkippedChunks = journal.Chunks.Count(chunk => chunk.State == "skipped"),
        };
    }

    public async Task<VideoVariantChunkCleanupV2RepairResult> RepairEntryFromEntryAsync(
        string backupRoot,
        int chunkId,
        string sourcePath,
        string targetPath,
        string expectedSha256,
        CancellationToken token = default
    )
    {
        string fullRoot = Path.GetFullPath(backupRoot);
        string sourceName = NormalizePath(sourcePath);
        string targetName = NormalizePath(targetPath);
        string expectedHash = expectedSha256.Trim().ToUpperInvariant();

        if (sourceName.Equals(targetName, StringComparison.Ordinal))
            throw new ArgumentException("Source and target paths must differ.");
        if (expectedHash.Length != 64 || expectedHash.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException(
                "Expected SHA256 must contain 64 hexadecimal characters.",
                nameof(expectedSha256)
            );

        string lockPath = Path.Combine(fullRoot, ".video-variant-cleanup.lock");
        string journalPath = Path.Combine(fullRoot, ".video-variant-cleanup.run.json");
        Directory.CreateDirectory(fullRoot);
        await using FileStream cleanupLock = new(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            1,
            FileOptions.Asynchronous
        );

        if (File.Exists(journalPath))
            throw new InvalidOperationException("Cleanup journal exists; repair cannot run.");

        string chunksRoot = Path.Combine(fullRoot, "chunks");
        string zipPath = Path.Combine(chunksRoot, $"{chunkId}.chunk.zip");
        string jsonPath = Path.Combine(chunksRoot, $"{chunkId}.chunk.json");
        string runId = Guid.NewGuid().ToString("N");
        string tempZip = $"{zipPath}.repair.{runId}.tmp";
        string tempJson = $"{jsonPath}.repair.{runId}.tmp";
        string backupZip = $"{zipPath}.repair.{runId}.bak";
        string backupJson = $"{jsonPath}.repair.{runId}.bak";

        try
        {
            Chunk chunk =
                JsonSerializer.Deserialize<Chunk>(
                    await File.ReadAllBytesAsync(jsonPath, token),
                    JsonOptions
                ) ?? throw new InvalidOperationException($"Chunk {chunkId}: JSON is invalid.");

            if (chunk.Id != chunkId)
                throw new InvalidOperationException($"Chunk {chunkId}: JSON ID mismatch.");

            List<ChunkData> sourceRows = chunk
                .Data.Where(data =>
                    NormalizePath(data.Path).Equals(sourceName, StringComparison.Ordinal)
                )
                .ToList();
            List<ChunkData> targetRows = chunk
                .Data.Where(data =>
                    NormalizePath(data.Path).Equals(targetName, StringComparison.Ordinal)
                )
                .ToList();

            if (sourceRows.Count != 1 || targetRows.Count != 1)
                throw new InvalidOperationException(
                    $"Chunk {chunkId}: source and target must each have one JSON row."
                );

            ChunkData sourceData = sourceRows[0];
            ChunkData targetData = targetRows[0];

            if (!string.Equals(targetData.Hash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Chunk {chunkId}: target JSON hash does not match expected SHA256."
                );
            if (!string.Equals(sourceData.Hash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Chunk {chunkId}: source JSON hash does not match expected SHA256."
                );
            if (sourceData.FileSize != targetData.FileSize)
                throw new InvalidOperationException(
                    $"Chunk {chunkId}: source and target JSON sizes differ."
                );

            await using (
                FileStream sourceStream = new(
                    zipPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    CopyBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan
                )
            )

            await using (
                FileStream targetStream = new(
                    tempZip,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    CopyBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan
                )
            )

            using (ZipArchive sourceZip = new(sourceStream, ZipArchiveMode.Read, leaveOpen: false))

            using (
                ZipArchive targetZip = new(targetStream, ZipArchiveMode.Create, leaveOpen: false)
            )
            {
                List<ZipArchiveEntry> sourceEntries = sourceZip
                    .Entries.Where(entry =>
                        NormalizePath(entry.FullName).Equals(sourceName, StringComparison.Ordinal)
                    )
                    .ToList();
                List<ZipArchiveEntry> targetEntries = sourceZip
                    .Entries.Where(entry =>
                        NormalizePath(entry.FullName).Equals(targetName, StringComparison.Ordinal)
                    )
                    .ToList();

                if (sourceEntries.Count != 1 || targetEntries.Count != 1)
                    throw new InvalidOperationException(
                        $"Chunk {chunkId}: source and target must each have one ZIP entry."
                    );

                ZipArchiveEntry sourceEntry = sourceEntries[0];

                await using (Stream hashInput = sourceEntry.Open())
                {
                    string sourceHash = Convert.ToHexString(
                        await SHA256.HashDataAsync(hashInput, token)
                    );

                    if (!sourceHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"Chunk {chunkId}: source ZIP hash does not match expected SHA256."
                        );
                }

                foreach (ZipArchiveEntry entry in sourceZip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    ZipArchiveEntry copy = targetZip.CreateEntry(
                        entry.FullName,
                        CompressionLevel.NoCompression
                    );
                    copy.LastWriteTime = entry.LastWriteTime;
                    copy.ExternalAttributes = entry.ExternalAttributes;
                    ZipArchiveEntry content = NormalizePath(entry.FullName)
                        .Equals(targetName, StringComparison.Ordinal)
                        ? sourceEntry
                        : entry;
                    await using Stream input = content.Open();
                    await using Stream output = copy.Open();
                    await input.CopyToAsync(output, CopyBufferSize, token);
                }
            }

            long targetSize;
            uint targetCrc32;
            string targetHash;

            await using (
                FileStream stream = new(tempZip, FileMode.Open, FileAccess.Read, FileShare.Read)
            )

            using (ZipArchive zip = new(stream, ZipArchiveMode.Read, leaveOpen: false))
            {
                ZipArchiveEntry entry = zip.Entries.Single(entry =>
                    NormalizePath(entry.FullName).Equals(targetName, StringComparison.Ordinal)
                );
                targetSize = entry.Length;
                targetCrc32 = entry.Crc32;
                await using Stream hashInput = entry.Open();
                targetHash = Convert.ToHexString(await SHA256.HashDataAsync(hashInput, token));
            }

            if (!targetHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Chunk {chunkId}: repaired target hash does not match expected SHA256."
                );

            targetData.FileSize = targetSize;
            targetData.Hash = targetHash;
            targetData.Crc32 = targetCrc32;
            await File.WriteAllBytesAsync(
                tempJson,
                JsonSerializer.SerializeToUtf8Bytes(chunk, JsonOptions),
                token
            );
            await ValidatePreparedAsync(tempZip, tempJson, chunkId, token);

            File.Move(zipPath, backupZip);

            try
            {
                File.Move(jsonPath, backupJson);
                File.Move(tempZip, zipPath);
                File.Move(tempJson, jsonPath);
                await ValidatePreparedAsync(zipPath, jsonPath, chunkId, token);
                string activeHash = await ReadZipEntryHashAsync(zipPath, targetName, token);

                if (!activeHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Chunk {chunkId}: active target hash does not match expected SHA256."
                    );
            }
            catch
            {
                RestoreRepairArtifacts(zipPath, jsonPath, backupZip, backupJson);
                throw;
            }

            DeleteIfExists(backupZip);
            DeleteIfExists(backupJson);
            return new VideoVariantChunkCleanupV2RepairResult
            {
                ChunkId = chunkId,
                TargetPath = targetName,
                FileSize = targetSize,
                Crc32 = targetCrc32,
                Hash = targetHash,
            };
        }
        catch
        {
            RestoreRepairArtifacts(zipPath, jsonPath, backupZip, backupJson);
            throw;
        }
        finally
        {
            DeleteIfExists(tempZip);
            DeleteIfExists(tempJson);
        }
    }

    private static async Task<PreflightChunk> InspectChunkAsync(
        string chunksRoot,
        VideoVariantChunkCleanupV2Chunk plan,
        IReadOnlyList<string> paths,
        bool committed,
        bool skipped,
        CancellationToken token
    )
    {
        string zipPath = Path.Combine(chunksRoot, $"{plan.Id}.chunk.zip");
        string jsonPath = Path.Combine(chunksRoot, $"{plan.Id}.chunk.json");
        List<string> errors = [];
        HashSet<string> removalPaths = plan
            .RemovePathIndices.Where(index => index >= 0 && index < paths.Count)
            .Select(index => paths[index])
            .ToHashSet(StringComparer.Ordinal);

        if (skipped)
            removalPaths.Clear();

        if (!File.Exists(zipPath) || !File.Exists(jsonPath))
        {
            if (!File.Exists(zipPath))
                errors.Add($"Chunk {plan.Id}: ZIP missing: {zipPath}");
            if (!File.Exists(jsonPath))
                errors.Add($"Chunk {plan.Id}: JSON missing: {jsonPath}");
            return new PreflightChunk(plan.Id, [], 0, 0, 0, errors);
        }

        FileInfo zipInfo = new(zipPath);
        FileInfo jsonInfo = new(jsonPath);

        if (
            !committed
            && (
                zipInfo.Length != plan.ZipLength
                || zipInfo.LastWriteTimeUtc.Ticks != plan.ZipLastWriteUtcTicks
            )
        )
        {
            errors.Add($"Chunk {plan.Id}: ZIP fingerprint changed.");
        }

        if (
            !committed
            && (
                jsonInfo.Length != plan.ManifestLength
                || jsonInfo.LastWriteTimeUtc.Ticks != plan.ManifestLastWriteUtcTicks
            )
        )
        {
            errors.Add($"Chunk {plan.Id}: JSON fingerprint changed.");
        }

        Chunk? chunk = null;

        try
        {
            byte[] json = await File.ReadAllBytesAsync(jsonPath, token);
            chunk = JsonSerializer.Deserialize<Chunk>(json, JsonOptions);
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            errors.Add($"Chunk {plan.Id}: JSON read failed: {ex.Message}");
        }

        if (chunk is null)
        {
            errors.Add($"Chunk {plan.Id}: JSON is invalid.");
            return new PreflightChunk(plan.Id, [], 0, 0, 0, errors);
        }

        string fileId = Path.GetFileName(jsonPath)
            .Replace(".chunk.json", "", StringComparison.Ordinal);

        if (!int.TryParse(fileId, out int parsedId) || parsedId != plan.Id || chunk.Id != plan.Id)
            errors.Add($"Chunk {plan.Id}: filename ID or JSON ID does not match plan.");

        List<string> jsonPaths = chunk.Data.Select(data => NormalizePath(data.Path)).ToList();
        AddNameErrors(plan.Id, "JSON", jsonPaths, errors);
        Dictionary<string, ChunkData> jsonByPath = new(StringComparer.Ordinal);

        foreach (ChunkData data in chunk.Data)
            jsonByPath.TryAdd(NormalizePath(data.Path), data);

        List<ZipEntryMetadata> zipEntries = [];

        try
        {
            await using FileStream stream = new(
                zipPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            );
            using ZipArchive zip = new(stream, ZipArchiveMode.Read, leaveOpen: false);

            foreach (
                ZipArchiveEntry entry in zip.Entries.Where(entry =>
                    !string.IsNullOrEmpty(entry.Name)
                )
            )
                zipEntries.Add(new ZipEntryMetadata(entry.FullName, entry.Length, entry.Crc32));
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            errors.Add($"Chunk {plan.Id}: ZIP read failed: {ex.Message}");
        }

        List<string> zipPaths = zipEntries.Select(entry => entry.Path).ToList();
        AddNameErrors(plan.Id, "ZIP", zipPaths, errors);
        Dictionary<string, ZipEntryMetadata> zipByPath = new(StringComparer.Ordinal);

        foreach (ZipEntryMetadata entry in zipEntries)
            zipByPath.TryAdd(entry.Path, entry);

        foreach (string path in jsonByPath.Keys.Except(zipByPath.Keys, StringComparer.Ordinal))
            errors.Add($"Chunk {plan.Id}: JSON entry missing from ZIP: {path}");

        foreach (string path in zipByPath.Keys.Except(jsonByPath.Keys, StringComparer.Ordinal))
            errors.Add($"Chunk {plan.Id}: ZIP entry missing from JSON: {path}");

        foreach ((string path, ChunkData data) in jsonByPath)
        {
            if (!zipByPath.TryGetValue(path, out ZipEntryMetadata? entry))
                continue;

            if (data.FileSize is null || data.FileSize.Value != entry.Length)
                errors.Add($"Chunk {plan.Id}: size mismatch: {path}");

            if (data.Crc32 is not null && data.Crc32.Value != entry.Crc32)
                errors.Add($"Chunk {plan.Id}: CRC mismatch: {path}");
        }

        long removalBytes = 0;

        foreach (string path in removalPaths)
        {
            bool inJson = jsonByPath.ContainsKey(path);
            bool inZip = zipByPath.TryGetValue(path, out ZipEntryMetadata? entry);

            if (committed && (inJson || inZip))
                errors.Add($"Chunk {plan.Id}: committed removal still exists: {path}");
            else if (!committed && (!inJson || !inZip))
                errors.Add($"Chunk {plan.Id}: removal missing from JSON or ZIP: {path}");
            else if (!committed)
                removalBytes += entry!.Length;
        }

        return new PreflightChunk(
            plan.Id,
            jsonByPath.Keys.ToHashSet(StringComparer.Ordinal),
            zipEntries.Count,
            committed ? 0 : removalPaths.Count,
            removalBytes,
            errors
        );
    }

    private static void AddNameErrors(
        int id,
        string source,
        IReadOnlyCollection<string> names,
        List<string> errors
    )
    {
        foreach (
            IGrouping<string, string> duplicate in names.GroupBy(
                name => name,
                StringComparer.Ordinal
            )
        )
        {
            if (duplicate.Count() > 1)
                errors.Add($"Chunk {id}: duplicate exact {source} name: {duplicate.Key}");
        }

        foreach (
            IGrouping<string, string> collision in names.GroupBy(
                name => name,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            if (collision.Distinct(StringComparer.Ordinal).Count() > 1)
                errors.Add($"Chunk {id}: OrdinalIgnoreCase {source} collision: {collision.Key}");
        }
    }

    private async Task<long> RewriteChunkAsync(
        VideoVariantChunkCleanupV2Manifest manifest,
        VideoVariantChunkCleanupV2Chunk plan,
        string runId,
        CancellationToken token
    )
    {
        ChunkArtifacts artifacts = GetArtifacts(manifest.BackupRoot, plan.Id, runId);
        HashSet<string> removals = plan
            .RemovePathIndices.Select(index => manifest.Paths[index])
            .ToHashSet(StringComparer.Ordinal);
        long removedBytes = 0;
        DeleteIfExists(artifacts.TempZip);
        DeleteIfExists(artifacts.TempJson);

        try
        {
            await using (
                FileStream sourceStream = new(
                    artifacts.ActiveZip,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    CopyBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan
                )
            )

            await using (
                FileStream targetStream = new(
                    artifacts.TempZip,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    CopyBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan
                )
            )

            using (ZipArchive source = new(sourceStream, ZipArchiveMode.Read, leaveOpen: false))

            using (ZipArchive target = new(targetStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (
                    ZipArchiveEntry entry in source.Entries.Where(entry =>
                        !string.IsNullOrEmpty(entry.Name)
                    )
                )
                {
                    token.ThrowIfCancellationRequested();

                    if (removals.Contains(entry.FullName))
                    {
                        removedBytes += entry.Length;
                        continue;
                    }

                    ZipArchiveEntry copy = target.CreateEntry(
                        entry.FullName,
                        CompressionLevel.NoCompression
                    );
                    copy.LastWriteTime = entry.LastWriteTime;
                    copy.ExternalAttributes = entry.ExternalAttributes;
                    await using Stream input = entry.Open();
                    await using Stream output = copy.Open();
                    await input.CopyToAsync(output, CopyBufferSize, token);
                }
            }

            Chunk chunk =
                JsonSerializer.Deserialize<Chunk>(
                    await File.ReadAllBytesAsync(artifacts.ActiveJson, token),
                    JsonOptions
                )
                ?? throw new InvalidOperationException(
                    $"Chunk {plan.Id}: invalid JSON during rewrite."
                );
            chunk.Data.RemoveAll(data => removals.Contains(NormalizePath(data.Path)));
            await File.WriteAllBytesAsync(
                artifacts.TempJson,
                JsonSerializer.SerializeToUtf8Bytes(chunk, JsonOptions),
                token
            );
            await ValidatePreparedAsync(artifacts.TempZip, artifacts.TempJson, plan.Id, token);

            File.Move(artifacts.ActiveZip, artifacts.BackupZip);

            try
            {
                File.Move(artifacts.ActiveJson, artifacts.BackupJson);
                File.Move(artifacts.TempZip, artifacts.ActiveZip);
                File.Move(artifacts.TempJson, artifacts.ActiveJson);
                await ValidatePreparedAsync(
                    artifacts.ActiveZip,
                    artifacts.ActiveJson,
                    plan.Id,
                    token
                );
            }
            catch
            {
                RestoreArtifacts(manifest.BackupRoot, plan.Id, runId);
                throw;
            }

            return removedBytes;
        }
        catch
        {
            RestoreArtifacts(manifest.BackupRoot, plan.Id, runId);
            throw;
        }
    }

    private static async Task ValidatePreparedAsync(
        string zipPath,
        string jsonPath,
        int id,
        CancellationToken token
    )
    {
        Chunk chunk =
            JsonSerializer.Deserialize<Chunk>(
                await File.ReadAllBytesAsync(jsonPath, token),
                JsonOptions
            ) ?? throw new InvalidOperationException($"Chunk {id}: prepared JSON is invalid.");

        if (chunk.Id != id)
            throw new InvalidOperationException($"Chunk {id}: prepared JSON ID mismatch.");

        List<string> errors = [];
        List<string> jsonPaths = chunk.Data.Select(data => NormalizePath(data.Path)).ToList();
        AddNameErrors(id, "JSON", jsonPaths, errors);
        Dictionary<string, ChunkData> data = new(StringComparer.Ordinal);

        foreach (ChunkData item in chunk.Data)
            data.TryAdd(NormalizePath(item.Path), item);

        List<ZipEntryMetadata> zipEntries = [];
        await using FileStream stream = new(
            zipPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        using ZipArchive zip = new(stream, ZipArchiveMode.Read, leaveOpen: false);

        foreach (
            ZipArchiveEntry entry in zip.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name))
        )
            zipEntries.Add(new ZipEntryMetadata(entry.FullName, entry.Length, entry.Crc32));

        AddNameErrors(id, "ZIP", zipEntries.Select(entry => entry.Path).ToList(), errors);
        Dictionary<string, ZipEntryMetadata> entries = new(StringComparer.Ordinal);

        foreach (ZipEntryMetadata entry in zipEntries)
            entries.TryAdd(entry.Path, entry);

        foreach (string path in data.Keys.Except(entries.Keys, StringComparer.Ordinal))
            errors.Add($"Chunk {id}: prepared JSON entry missing from ZIP: {path}");

        foreach (string path in entries.Keys.Except(data.Keys, StringComparer.Ordinal))
            errors.Add($"Chunk {id}: prepared ZIP entry missing from JSON: {path}");

        foreach ((string path, ChunkData item) in data)
        {
            if (!entries.TryGetValue(path, out ZipEntryMetadata? entry))
                continue;
            if (item.FileSize is null || item.FileSize.Value != entry.Length)
                errors.Add($"Chunk {id}: prepared size mismatch: {path}");
            if (item.Crc32 is not null && item.Crc32.Value != entry.Crc32)
                errors.Add($"Chunk {id}: prepared CRC mismatch: {path}");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
    }

    private static async Task RecoverAsync(
        VideoVariantChunkCleanupV2Manifest manifest,
        VideoVariantChunkCleanupV2Journal journal,
        IReadOnlyDictionary<int, VideoVariantChunkCleanupV2Chunk> plans,
        string journalPath,
        CancellationToken token
    )
    {
        foreach (VideoVariantChunkCleanupV2JournalChunk state in journal.Chunks)
        {
            ChunkArtifacts artifacts = GetArtifacts(manifest.BackupRoot, state.Id, journal.RunId);
            bool hasArtifacts =
                File.Exists(artifacts.TempZip)
                || File.Exists(artifacts.TempJson)
                || File.Exists(artifacts.BackupZip)
                || File.Exists(artifacts.BackupJson);

            if (!hasArtifacts)
                continue;

            bool activeValid = false;

            if (File.Exists(artifacts.ActiveZip) && File.Exists(artifacts.ActiveJson))
            {
                try
                {
                    await ValidatePreparedAsync(
                        artifacts.ActiveZip,
                        artifacts.ActiveJson,
                        state.Id,
                        token
                    );
                    HashSet<string> activePaths = await ReadChunkPathsAsync(
                        artifacts.ActiveJson,
                        token
                    );
                    activeValid = plans[state.Id]
                        .RemovePathIndices.Select(index => manifest.Paths[index])
                        .All(path => !activePaths.Contains(path));
                }
                catch (Exception ex)
                    when (ex
                            is IOException
                                or JsonException
                                or InvalidDataException
                                or InvalidOperationException
                    )
                {
                    activeValid = false;
                }
            }

            if (
                activeValid
                && File.Exists(artifacts.BackupZip)
                && File.Exists(artifacts.BackupJson)
            )
            {
                state.State = "committed";
                DeleteIfExists(artifacts.BackupZip);
                DeleteIfExists(artifacts.BackupJson);
                DeleteIfExists(artifacts.TempZip);
                DeleteIfExists(artifacts.TempJson);
                WriteJsonAtomic(journalPath, journal);
            }
            else
            {
                RestoreArtifacts(manifest.BackupRoot, state.Id, journal.RunId);
                state.State = "pending";
                WriteJsonAtomic(journalPath, journal);
            }
        }
    }

    private static async Task<HashSet<string>> ReadChunkPathsAsync(
        string jsonPath,
        CancellationToken token
    )
    {
        Chunk chunk =
            JsonSerializer.Deserialize<Chunk>(
                await File.ReadAllBytesAsync(jsonPath, token),
                JsonOptions
            ) ?? throw new InvalidOperationException("Chunk JSON is invalid.");
        return chunk
            .Data.Select(data => NormalizePath(data.Path))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<string> ReadZipEntryHashAsync(
        string zipPath,
        string entryPath,
        CancellationToken token
    )
    {
        await using FileStream stream = new(
            zipPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        using ZipArchive zip = new(stream, ZipArchiveMode.Read, leaveOpen: false);
        ZipArchiveEntry entry = zip.Entries.Single(item =>
            NormalizePath(item.FullName).Equals(entryPath, StringComparison.Ordinal)
        );
        await using Stream input = entry.Open();
        return Convert.ToHexString(await SHA256.HashDataAsync(input, token));
    }

    private static void RestoreRepairArtifacts(
        string zipPath,
        string jsonPath,
        string backupZip,
        string backupJson
    )
    {
        if (File.Exists(backupZip))
        {
            DeleteIfExists(zipPath);
            File.Move(backupZip, zipPath);
        }

        if (File.Exists(backupJson))
        {
            DeleteIfExists(jsonPath);
            File.Move(backupJson, jsonPath);
        }
    }

    private static void RestoreArtifacts(string backupRoot, int id, string runId)
    {
        ChunkArtifacts artifacts = GetArtifacts(backupRoot, id, runId);

        if (File.Exists(artifacts.BackupZip))
        {
            DeleteIfExists(artifacts.ActiveZip);
            File.Move(artifacts.BackupZip, artifacts.ActiveZip);
        }

        if (File.Exists(artifacts.BackupJson))
        {
            DeleteIfExists(artifacts.ActiveJson);
            File.Move(artifacts.BackupJson, artifacts.ActiveJson);
        }

        DeleteIfExists(artifacts.TempZip);
        DeleteIfExists(artifacts.TempJson);
    }

    private static void CleanupCommittedArtifacts(string backupRoot, int id, string runId)
    {
        ChunkArtifacts artifacts = GetArtifacts(backupRoot, id, runId);
        DeleteIfExists(artifacts.BackupZip);
        DeleteIfExists(artifacts.BackupJson);
        DeleteIfExists(artifacts.TempZip);
        DeleteIfExists(artifacts.TempJson);
    }

    private static ChunkArtifacts GetArtifacts(string backupRoot, int id, string runId)
    {
        string chunksRoot = Path.Combine(backupRoot, "chunks");
        string zip = Path.Combine(chunksRoot, $"{id}.chunk.zip");
        string json = Path.Combine(chunksRoot, $"{id}.chunk.json");
        return new ChunkArtifacts(
            zip,
            json,
            $"{zip}.cleanup.{runId}.tmp",
            $"{json}.cleanup.{runId}.tmp",
            $"{zip}.cleanup.{runId}.bak",
            $"{json}.cleanup.{runId}.bak"
        );
    }

    private void ValidateManifest(VideoVariantChunkCleanupV2Manifest manifest, bool verifyHash)
    {
        if (manifest.Version != 2)
            throw new InvalidOperationException("Video cleanup manifest version must be 2.");
        if (
            string.IsNullOrWhiteSpace(manifest.BackupRoot)
            || !Path.IsPathFullyQualified(manifest.BackupRoot)
        )
            throw new InvalidOperationException("Manifest BackupRoot must be absolute.");
        if (manifest.Paths.Count != manifest.Paths.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidOperationException("Manifest path table contains duplicates.");
        if (manifest.Chunks.Select(chunk => chunk.Id).Distinct().Count() != manifest.Chunks.Count)
            throw new InvalidOperationException("Manifest contains duplicate chunk IDs.");

        foreach (VideoVariantChunkCleanupV2Variant item in manifest.Variants)
        {
            ValidatePathIndex(item.RemovePathIndex, manifest.Paths.Count);
            ValidatePathIndex(item.KeepPathIndex, manifest.Paths.Count);
        }

        foreach (VideoVariantChunkCleanupV2Chunk chunk in manifest.Chunks)
        {
            if (chunk.RemovePathIndices.Count != chunk.RemovePathIndices.Distinct().Count())
                throw new InvalidOperationException(
                    $"Chunk {chunk.Id} has duplicate removal indices."
                );

            foreach (int index in chunk.RemovePathIndices)
                ValidatePathIndex(index, manifest.Paths.Count);
        }

        HashSet<int> variantRemovals = manifest
            .Variants.Select(item => item.RemovePathIndex)
            .ToHashSet();
        if (
            manifest
                .Chunks.SelectMany(chunk => chunk.RemovePathIndices)
                .Any(index => !variantRemovals.Contains(index))
        )
            throw new InvalidOperationException(
                "Chunk removal does not reference a manifest variant."
            );

        if (
            verifyHash
            && !CalculateApprovalHash(manifest)
                .Equals(manifest.ApprovalHash, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException("Video cleanup manifest approval hash is invalid.");
        }
    }

    private static void ValidatePathIndex(int index, int count)
    {
        if (index < 0 || index >= count)
            throw new InvalidOperationException($"Manifest path index is out of range: {index}");
    }

    private static void ValidateParallelism(int parallelism)
    {
        if (parallelism < 1)
            throw new ArgumentOutOfRangeException(
                nameof(parallelism),
                "Parallelism must be positive."
            );
    }

    private static void ThrowIfPreflightFailed(VideoVariantChunkCleanupV2PreflightResult preflight)
    {
        if (!preflight.IsValid)
            throw new InvalidOperationException(
                "Preflight failed:"
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, preflight.Errors)
            );
    }

    private static void MarkSkippedChunks(
        VideoVariantChunkCleanupV2Journal journal,
        IReadOnlySet<int>? skipChunkIds
    )
    {
        if (skipChunkIds is null || skipChunkIds.Count == 0)
            return;

        Dictionary<int, VideoVariantChunkCleanupV2JournalChunk> states =
            journal.Chunks.ToDictionary(chunk => chunk.Id);

        foreach (int id in skipChunkIds)
        {
            if (!states.TryGetValue(id, out VideoVariantChunkCleanupV2JournalChunk? state))
                throw new InvalidOperationException($"Cannot skip unknown cleanup chunk {id}.");

            if (state.State == "committed")
                throw new InvalidOperationException($"Cannot skip committed cleanup chunk {id}.");

            state.State = "skipped";
        }
    }

    private static void WriteJsonAtomic<T>(string path, T value)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllBytes(tempPath, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            DeleteIfExists(tempPath);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');

    private sealed record PreviewVideo(
        int ChunkId,
        string Path,
        string Group,
        int Width,
        int Height
    );

    private sealed record PreviewChunk(
        int Id,
        long ZipLength,
        long ZipLastWriteUtcTicks,
        long ManifestLength,
        long ManifestLastWriteUtcTicks,
        List<PreviewVideo> Videos
    );

    private sealed record ZipEntryMetadata(string Path, long Length, uint Crc32);

    private sealed record PreflightChunk(
        int Id,
        HashSet<string> Paths,
        long EntryCount,
        long RemovalCount,
        long RemovalBytes,
        List<string> Errors
    );

    private sealed record ChunkArtifacts(
        string ActiveZip,
        string ActiveJson,
        string TempZip,
        string TempJson,
        string BackupZip,
        string BackupJson
    );
}

public sealed class VideoVariantChunkCleanupV2Manifest
{
    public int Version { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public required string BackupRoot { get; set; }
    public string ApprovalHash { get; set; } = string.Empty;
    public List<string> Paths { get; set; } = [];
    public List<VideoVariantChunkCleanupV2Variant> Variants { get; set; } = [];
    public List<VideoVariantChunkCleanupV2Chunk> Chunks { get; set; } = [];
}

public sealed class VideoVariantChunkCleanupV2Variant
{
    public int RemovePathIndex { get; set; }
    public int KeepPathIndex { get; set; }
}

public sealed class VideoVariantChunkCleanupV2Chunk
{
    public int Id { get; set; }
    public long ZipLength { get; set; }
    public long ZipLastWriteUtcTicks { get; set; }
    public long ManifestLength { get; set; }
    public long ManifestLastWriteUtcTicks { get; set; }
    public List<int> RemovePathIndices { get; set; } = [];
}

public sealed class VideoVariantChunkCleanupV2PreflightResult
{
    public int Chunks { get; set; }
    public long Entries { get; set; }
    public long Removals { get; set; }
    public long Bytes { get; set; }
    public List<string> Errors { get; set; } = [];
    public bool IsValid => Errors.Count == 0;
}

public sealed class VideoVariantChunkCleanupV2ApplyResult
{
    public int Chunks { get; set; }
    public long Removals { get; set; }
    public long Bytes { get; set; }
    public int SkippedChunks { get; set; }
}

public sealed class VideoVariantChunkCleanupV2RepairResult
{
    public int ChunkId { get; set; }
    public required string TargetPath { get; set; }
    public long FileSize { get; set; }
    public uint Crc32 { get; set; }
    public required string Hash { get; set; }
}

public sealed class VideoVariantChunkCleanupV2PreviewProgress
{
    public int CompletedChunks { get; set; }
    public int TotalChunks { get; set; }
    public long Entries { get; set; }
    public long VideoEntries { get; set; }
    public TimeSpan Elapsed { get; set; }
    public int Parallelism { get; set; }
}

public sealed class VideoVariantChunkCleanupV2PreflightProgress
{
    public int CompletedChunks { get; set; }
    public int TotalChunks { get; set; }
    public long Entries { get; set; }
    public long Removals { get; set; }
    public long Bytes { get; set; }
    public int Errors { get; set; }
    public TimeSpan Elapsed { get; set; }
    public int Parallelism { get; set; }
}

public sealed class VideoVariantChunkCleanupV2ApplyProgress
{
    public int CommittedChunks { get; set; }
    public int TotalChunks { get; set; }
    public int CurrentChunkId { get; set; }
    public int RemovedEntries { get; set; }
    public long RemovedBytes { get; set; }
}

public sealed class VideoVariantChunkCleanupV2Journal
{
    public required string ApprovalHash { get; set; }
    public required string RunId { get; set; }
    public List<VideoVariantChunkCleanupV2JournalChunk> Chunks { get; set; } = [];
}

public sealed class VideoVariantChunkCleanupV2JournalChunk
{
    public int Id { get; set; }
    public string State { get; set; } = "pending";
}
