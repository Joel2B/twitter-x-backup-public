using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.Maintenance;

public sealed class VideoVariantCleanupService
{
    private static readonly Regex VideoPathPattern = new(
        @"^(?<post>[^/]+)/video/(?<media>[^/]+)/mp4/(?<video>[^/]+)/(?<width>[1-9]\d*)x(?<height>[1-9]\d*)\.mp4$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public VideoVariantCleanupManifest Preview(
        IReadOnlyCollection<string> mediaRoots,
        string backupRoot,
        bool includeDirect = true,
        bool includeChunks = true,
        int chunkParallelism = 8,
        Action<VideoVariantPreviewProgress>? progress = null
    )
    {
        List<VideoVariantCopy> copies = [];
        List<string> scanErrors = [];

        foreach (string mediaRoot in mediaRoots)
            AddFileCopies(copies, scanErrors, mediaRoot, "media", mediaRoot);

        string directRoot = Path.Combine(backupRoot, "direct");

        if (includeDirect)
            AddFileCopies(copies, scanErrors, directRoot, "backup-direct", directRoot);

        if (includeChunks)
        {
            AddChunkCopies(
                copies,
                scanErrors,
                Path.Combine(backupRoot, "chunks"),
                chunkParallelism,
                progress
            );
        }

        List<VideoVariantCleanupCandidate> candidates = [];
        Dictionary<string, List<VideoVariantCopy>> copiesByPath = copies
            .GroupBy(copy => copy.LogicalPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase
            );

        foreach (
            IGrouping<string, VideoVariantCopy> group in copies.GroupBy(
                copy => copy.GroupKey,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            List<VideoVariantCopy> variants = group
                .GroupBy(copy => copy.LogicalPath, StringComparer.OrdinalIgnoreCase)
                .Select(items => items.First())
                .OrderByDescending(copy => (long)copy.Width * copy.Height)
                .ThenByDescending(copy => copy.Width)
                .ThenByDescending(copy => copy.Height)
                .ToList();

            if (variants.Count < 2)
                continue;

            VideoVariantCopy keep = variants[0];

            foreach (VideoVariantCopy remove in variants.Skip(1))
            {
                if (remove.Width > keep.Width || remove.Height > keep.Height)
                    continue;

                List<VideoVariantCopy> removeCopies = copiesByPath[remove.LogicalPath];

                candidates.Add(
                    new VideoVariantCleanupCandidate
                    {
                        GroupKey = group.Key,
                        KeepLogicalPath = keep.LogicalPath,
                        RemoveLogicalPath = remove.LogicalPath,
                        KeepResolution = $"{keep.Width}x{keep.Height}",
                        RemoveResolution = $"{remove.Width}x{remove.Height}",
                        Copies = removeCopies,
                    }
                );
            }
        }

        VideoVariantCleanupManifest manifest = new()
        {
            Version = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
            MediaRoots = mediaRoots.Select(Path.GetFullPath).ToList(),
            BackupRoot = Path.GetFullPath(backupRoot),
            ScanErrors = scanErrors,
            Candidates = candidates
                .OrderBy(candidate => candidate.GroupKey, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.RemoveLogicalPath, StringComparer.Ordinal)
                .ToList(),
        };
        manifest.ApprovalHash = CalculateApprovalHash(manifest);
        return manifest;
    }

    public VideoVariantChunkBenchmark BenchmarkChunk(string backupRoot, int chunkId)
    {
        string chunksRoot = Path.Combine(Path.GetFullPath(backupRoot), "chunks");
        string manifestPath = Path.Combine(chunksRoot, $"{chunkId}.chunk.json");

        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Chunk manifest not found.", manifestPath);

        Stopwatch timer = Stopwatch.StartNew();
        byte[] json = File.ReadAllBytes(manifestPath);
        TimeSpan readTime = timer.Elapsed;

        timer.Restart();
        Chunk chunk =
            JsonSerializer.Deserialize<Chunk>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid chunk manifest: {manifestPath}");
        TimeSpan parseTime = timer.Elapsed;

        timer.Restart();
        VideoVariantChunkAnalysis sequential = AnalyzeChunkEntries(chunk.Data, parallel: false);
        TimeSpan sequentialTime = timer.Elapsed;

        timer.Restart();
        VideoVariantChunkAnalysis parallel = AnalyzeChunkEntries(chunk.Data, parallel: true);
        TimeSpan parallelTime = timer.Elapsed;

        if (
            sequential.Mp4Entries != parallel.Mp4Entries
            || sequential.Groups != parallel.Groups
            || sequential.Candidates != parallel.Candidates
        )
        {
            throw new InvalidOperationException("Sequential and parallel chunk analyses disagree.");
        }

        return new VideoVariantChunkBenchmark
        {
            ChunkId = chunkId,
            JsonBytes = json.LongLength,
            Entries = chunk.Data.Count,
            Mp4Entries = sequential.Mp4Entries,
            Groups = sequential.Groups,
            Candidates = sequential.Candidates,
            ReadTime = readTime,
            ParseTime = parseTime,
            SequentialTime = sequentialTime,
            ParallelTime = parallelTime,
        };
    }

    public void SaveManifest(VideoVariantCleanupManifest manifest, string outputPath)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));

        if (directory is not null)
            Directory.CreateDirectory(directory);

        File.WriteAllText(outputPath, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    public VideoVariantCleanupManifest LoadManifest(string manifestPath)
    {
        VideoVariantCleanupManifest manifest =
            JsonSerializer.Deserialize<VideoVariantCleanupManifest>(
                File.ReadAllText(manifestPath),
                JsonOptions
            ) ?? throw new InvalidOperationException("Invalid video cleanup manifest.");

        string expectedHash = CalculateApprovalHash(manifest);

        if (!expectedHash.Equals(manifest.ApprovalHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Video cleanup manifest approval hash is invalid.");

        return manifest;
    }

    public VideoVariantCleanupResult Apply(VideoVariantCleanupManifest manifest)
    {
        if (manifest.ScanErrors.Count > 0)
            throw new InvalidOperationException("Cannot apply a manifest with scan errors.");

        ValidateManifestPaths(manifest);

        foreach (VideoVariantCleanupCandidate candidate in manifest.Candidates)
        {
            if (!KeepVariantExists(manifest, candidate.KeepLogicalPath))
            {
                throw new InvalidOperationException(
                    $"Kept video variant is missing: {candidate.KeepLogicalPath}"
                );
            }

            foreach (VideoVariantCopy copy in candidate.Copies)
                ValidateCopy(copy);
        }

        int removedCopies = 0;
        long removedBytes = 0;

        foreach (
            IGrouping<string, VideoVariantCopy> chunkGroup in manifest
                .Candidates.SelectMany(candidate => candidate.Copies)
                .Where(copy => copy.Kind == "backup-chunk")
                .GroupBy(copy => copy.ContainerPath!, StringComparer.OrdinalIgnoreCase)
        )
        {
            RewriteChunk(chunkGroup.Key, chunkGroup.ToList());
            removedCopies += chunkGroup.Count();
            removedBytes += chunkGroup.Sum(copy => copy.Size);
        }

        foreach (
            VideoVariantCopy copy in manifest
                .Candidates.SelectMany(candidate => candidate.Copies)
                .Where(copy => copy.Kind is "media" or "backup-direct")
        )
        {
            File.Delete(copy.FullPath!);
            removedCopies++;
            removedBytes += copy.Size;
        }

        return new VideoVariantCleanupResult
        {
            RemovedVariants = manifest.Candidates.Count,
            RemovedCopies = removedCopies,
            RemovedBytes = removedBytes,
        };
    }

    private static void AddFileCopies(
        List<VideoVariantCopy> copies,
        List<string> scanErrors,
        string root,
        string kind,
        string boundaryRoot
    )
    {
        if (!Directory.Exists(root))
            return;

        string fullBoundary = Path.GetFullPath(boundaryRoot);

        foreach (string path in EnumerateFiles(root, "*.mp4", scanErrors))
        {
            string fullPath = Path.GetFullPath(path);

            if (!IsWithinRoot(fullPath, fullBoundary))
                continue;

            string logicalPath = NormalizeLogicalPath(Path.GetRelativePath(root, fullPath));
            Match match = VideoPathPattern.Match(logicalPath);

            if (!match.Success)
                continue;

            FileInfo file = new(fullPath);
            copies.Add(
                CreateCopy(match, logicalPath, kind, file.Length, file.LastWriteTimeUtc, fullPath)
            );
        }
    }

    private static void AddChunkCopies(
        List<VideoVariantCopy> copies,
        List<string> scanErrors,
        string chunksRoot,
        int parallelism,
        Action<VideoVariantPreviewProgress>? progress
    )
    {
        if (!Directory.Exists(chunksRoot))
            return;

        List<string> manifestPaths = EnumerateFiles(chunksRoot, "*.chunk.json", scanErrors)
            .ToList();
        object mergeLock = new();
        int completedChunks = 0;
        int entries = 0;
        int mp4Entries = 0;
        Stopwatch timer = Stopwatch.StartNew();

        Parallel.ForEach(
            manifestPaths,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            manifestPath =>
            {
                List<VideoVariantCopy> localCopies = [];
                string? scanError = null;
                int localEntries = 0;

                try
                {
                    Chunk? chunk = JsonSerializer.Deserialize<Chunk>(
                        File.ReadAllText(manifestPath),
                        JsonOptions
                    );

                    if (chunk is not null)
                    {
                        localEntries = chunk.Data.Count;
                        string zipPath = Path.Combine(chunksRoot, $"{chunk.Id}.chunk.zip");
                        bool zipExists = File.Exists(zipPath);

                        if (!zipExists)
                        {
                            scanError = $"Missing chunk ZIP: {zipPath}";
                        }
                        else
                        {
                            DateTime zipLastWriteUtc = File.GetLastWriteTimeUtc(zipPath);

                            foreach (ChunkData data in chunk.Data)
                            {
                                string logicalPath = NormalizeLogicalPath(data.Path);
                                Match match = VideoPathPattern.Match(logicalPath);

                                if (!match.Success || data.FileSize is null)
                                    continue;

                                VideoVariantCopy copy = CreateCopy(
                                    match,
                                    logicalPath,
                                    "backup-chunk",
                                    data.FileSize.Value,
                                    zipLastWriteUtc,
                                    null
                                );
                                copy.ContainerPath = Path.GetFullPath(zipPath);
                                copy.ManifestPath = Path.GetFullPath(manifestPath);
                                copy.EntryPath = logicalPath;
                                localCopies.Add(copy);
                            }
                        }
                    }
                }
                catch (Exception ex)
                    when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    scanError = $"{manifestPath}: {ex.Message}";
                }

                lock (mergeLock)
                {
                    copies.AddRange(localCopies);

                    if (scanError is not null)
                        scanErrors.Add(scanError);

                    completedChunks++;
                    entries += localEntries;
                    mp4Entries += localCopies.Count;
                    progress?.Invoke(
                        new VideoVariantPreviewProgress
                        {
                            CompletedChunks = completedChunks,
                            TotalChunks = manifestPaths.Count,
                            Entries = entries,
                            Mp4Entries = mp4Entries,
                            Elapsed = timer.Elapsed,
                            Parallelism = parallelism,
                        }
                    );
                }
            }
        );
    }

    private static VideoVariantCopy CreateCopy(
        Match match,
        string logicalPath,
        string kind,
        long size,
        DateTime lastWriteUtc,
        string? fullPath
    ) =>
        new()
        {
            Kind = kind,
            GroupKey = string.Join('/', match.Groups["post"].Value, match.Groups["media"].Value),
            LogicalPath = logicalPath,
            Width = int.Parse(match.Groups["width"].Value),
            Height = int.Parse(match.Groups["height"].Value),
            Size = size,
            LastWriteUtc = lastWriteUtc,
            FullPath = fullPath,
        };

    private static VideoVariantChunkAnalysis AnalyzeChunkEntries(
        IReadOnlyCollection<ChunkData> entries,
        bool parallel
    )
    {
        IEnumerable<ChunkData> source = parallel ? entries.AsParallel() : entries;
        List<(string GroupKey, int Width, int Height)> videos = source
            .Select(data => VideoPathPattern.Match(NormalizeLogicalPath(data.Path)))
            .Where(match => match.Success)
            .Select(match =>
                (
                    GroupKey: string.Join(
                        '/',
                        match.Groups["post"].Value,
                        match.Groups["media"].Value
                    ),
                    Width: int.Parse(match.Groups["width"].Value),
                    Height: int.Parse(match.Groups["height"].Value)
                )
            )
            .ToList();
        int candidates = videos
            .GroupBy(video => video.GroupKey, StringComparer.OrdinalIgnoreCase)
            .Sum(group =>
            {
                List<(string GroupKey, int Width, int Height)> variants = group
                    .DistinctBy(video => (video.Width, video.Height))
                    .OrderByDescending(video => (long)video.Width * video.Height)
                    .ThenByDescending(video => video.Width)
                    .ThenByDescending(video => video.Height)
                    .ToList();

                if (variants.Count < 2)
                    return 0;

                (string GroupKey, int Width, int Height) = variants[0];
                return variants
                    .Skip(1)
                    .Count(remove => remove.Width <= Width && remove.Height <= Height);
            });

        return new VideoVariantChunkAnalysis(
            videos.Count,
            videos
                .Select(video => video.GroupKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            candidates
        );
    }

    private static void ValidateManifestPaths(VideoVariantCleanupManifest manifest)
    {
        foreach (VideoVariantCopy copy in manifest.Candidates.SelectMany(item => item.Copies))
        {
            if (copy.Kind == "media")
            {
                if (
                    copy.FullPath is null
                    || !manifest.MediaRoots.Any(root => IsWithinRoot(copy.FullPath, root))
                )
                {
                    throw new InvalidOperationException(
                        $"Media path escaped configured roots: {copy.FullPath}"
                    );
                }
            }
            else if (copy.Kind == "backup-direct")
            {
                string directRoot = Path.Combine(manifest.BackupRoot, "direct");

                if (copy.FullPath is null || !IsWithinRoot(copy.FullPath, directRoot))
                    throw new InvalidOperationException(
                        $"Backup path escaped direct root: {copy.FullPath}"
                    );
            }
            else if (
                copy.Kind == "backup-chunk"
                && (
                    copy.ContainerPath is null
                    || copy.ManifestPath is null
                    || !IsWithinRoot(
                        copy.ContainerPath,
                        Path.Combine(manifest.BackupRoot, "chunks")
                    )
                    || !IsWithinRoot(copy.ManifestPath, Path.Combine(manifest.BackupRoot, "chunks"))
                )
            )
            {
                throw new InvalidOperationException("Chunk path escaped backup root.");
            }
        }
    }

    private static void ValidateCopy(VideoVariantCopy copy)
    {
        if (copy.Kind == "backup-chunk")
        {
            if (!File.Exists(copy.ContainerPath) || !File.Exists(copy.ManifestPath))
                throw new InvalidOperationException($"Backup chunk changed: {copy.ContainerPath}");

            if (File.GetLastWriteTimeUtc(copy.ContainerPath) != copy.LastWriteUtc)
                throw new InvalidOperationException(
                    $"Backup chunk timestamp changed: {copy.ContainerPath}"
                );

            using ZipArchive zip = ZipFile.OpenRead(copy.ContainerPath);
            ZipArchiveEntry? entry = zip.GetEntry(copy.EntryPath!);

            if (entry is null || entry.Length != copy.Size)
                throw new InvalidOperationException(
                    $"Backup chunk entry changed: {copy.EntryPath}"
                );

            return;
        }

        if (copy.FullPath is null || !File.Exists(copy.FullPath))
            throw new InvalidOperationException($"Video copy changed: {copy.FullPath}");

        FileInfo file = new(copy.FullPath);

        if (file.Length != copy.Size || file.LastWriteTimeUtc != copy.LastWriteUtc)
            throw new InvalidOperationException($"Video copy changed: {copy.FullPath}");
    }

    private static bool KeepVariantExists(VideoVariantCleanupManifest manifest, string logicalPath)
    {
        string relativePath = logicalPath.Replace('/', Path.DirectorySeparatorChar);

        if (manifest.MediaRoots.Any(root => File.Exists(Path.Combine(root, relativePath))))
            return true;

        if (File.Exists(Path.Combine(manifest.BackupRoot, "direct", relativePath)))
            return true;

        string chunksRoot = Path.Combine(manifest.BackupRoot, "chunks");

        if (!Directory.Exists(chunksRoot))
            return false;

        foreach (string zipPath in Directory.EnumerateFiles(chunksRoot, "*.chunk.zip"))
        {
            using ZipArchive zip = ZipFile.OpenRead(zipPath);

            if (zip.GetEntry(logicalPath) is not null)
                return true;
        }

        return false;
    }

    private static void RewriteChunk(string zipPath, IReadOnlyCollection<VideoVariantCopy> copies)
    {
        string manifestPath = copies.Select(copy => copy.ManifestPath).Distinct().Single()!;
        string tempZip = zipPath + ".cleanup.tmp";
        string tempManifest = manifestPath + ".cleanup.tmp";
        string backupZip = zipPath + ".cleanup.bak";
        string backupManifest = manifestPath + ".cleanup.bak";

        File.Copy(zipPath, tempZip, overwrite: true);

        using (ZipArchive zip = ZipFile.Open(tempZip, ZipArchiveMode.Update))
        {
            foreach (VideoVariantCopy copy in copies)
            {
                ZipArchiveEntry? entry = zip.GetEntry(copy.EntryPath!);

                if (entry is null)
                    throw new InvalidOperationException(
                        $"Backup entry disappeared: {copy.EntryPath}"
                    );

                entry.Delete();
            }
        }

        Chunk chunk =
            JsonSerializer.Deserialize<Chunk>(File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new InvalidOperationException($"Invalid chunk manifest: {manifestPath}");
        HashSet<string> removed = copies
            .Select(copy => copy.LogicalPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        chunk.Data.RemoveAll(data => removed.Contains(NormalizeLogicalPath(data.Path)));
        File.WriteAllText(tempManifest, JsonSerializer.Serialize(chunk, JsonOptions));

        File.Replace(tempZip, zipPath, backupZip, ignoreMetadataErrors: true);

        try
        {
            File.Replace(tempManifest, manifestPath, backupManifest, ignoreMetadataErrors: true);
            File.Delete(backupZip);
            File.Delete(backupManifest);
        }
        catch
        {
            File.Replace(backupZip, zipPath, null, ignoreMetadataErrors: true);
            throw;
        }
    }

    private static string CalculateApprovalHash(VideoVariantCleanupManifest manifest)
    {
        string approvalHash = manifest.ApprovalHash;
        manifest.ApprovalHash = string.Empty;
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        manifest.ApprovalHash = approvalHash;
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    private static string NormalizeLogicalPath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private static IEnumerable<string> EnumerateFiles(
        string root,
        string pattern,
        List<string> scanErrors
    )
    {
        Stack<string> pending = new();
        pending.Push(root);

        while (pending.TryPop(out string? directory))
        {
            string[] files;
            string[] directories;

            try
            {
                files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
                directories = Directory.GetDirectories(
                    directory,
                    "*",
                    SearchOption.TopDirectoryOnly
                );
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                scanErrors.Add($"{directory}: {ex.Message}");
                continue;
            }

            foreach (string file in files)
                yield return file;

            foreach (string child in directories)
                pending.Push(child);
        }
    }

    private static bool IsWithinRoot(string path, string root)
    {
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(
                fullRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            );
    }
}

public sealed class VideoVariantCleanupManifest
{
    public int Version { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public string ApprovalHash { get; set; } = string.Empty;
    public List<string> MediaRoots { get; set; } = [];
    public required string BackupRoot { get; set; }
    public List<string> ScanErrors { get; set; } = [];
    public List<VideoVariantCleanupCandidate> Candidates { get; set; } = [];
}

public sealed class VideoVariantCleanupCandidate
{
    public required string GroupKey { get; set; }
    public required string KeepLogicalPath { get; set; }
    public required string RemoveLogicalPath { get; set; }
    public required string KeepResolution { get; set; }
    public required string RemoveResolution { get; set; }
    public List<VideoVariantCopy> Copies { get; set; } = [];
}

public sealed class VideoVariantCopy
{
    public required string Kind { get; set; }
    public required string GroupKey { get; set; }
    public required string LogicalPath { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long Size { get; set; }
    public DateTime LastWriteUtc { get; set; }
    public string? FullPath { get; set; }
    public string? ContainerPath { get; set; }
    public string? ManifestPath { get; set; }
    public string? EntryPath { get; set; }
}

public sealed class VideoVariantCleanupResult
{
    public int RemovedVariants { get; set; }
    public int RemovedCopies { get; set; }
    public long RemovedBytes { get; set; }
}

public sealed class VideoVariantPreviewProgress
{
    public int CompletedChunks { get; set; }
    public int TotalChunks { get; set; }
    public int Entries { get; set; }
    public int Mp4Entries { get; set; }
    public TimeSpan Elapsed { get; set; }
    public int Parallelism { get; set; }
}

public sealed class VideoVariantChunkBenchmark
{
    public int ChunkId { get; set; }
    public long JsonBytes { get; set; }
    public int Entries { get; set; }
    public int Mp4Entries { get; set; }
    public int Groups { get; set; }
    public int Candidates { get; set; }
    public TimeSpan ReadTime { get; set; }
    public TimeSpan ParseTime { get; set; }
    public TimeSpan SequentialTime { get; set; }
    public TimeSpan ParallelTime { get; set; }
}

internal readonly record struct VideoVariantChunkAnalysis(
    int Mp4Entries,
    int Groups,
    int Candidates
);
