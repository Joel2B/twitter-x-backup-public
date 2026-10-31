using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Backup.Infrastructure.Media.Maintenance;
using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Tests;

public sealed class VideoVariantChunkCleanupV2ServiceTests : IDisposable
{
    private const string Low = "post-1/video/media-1/mp4/video-low/320x180.mp4";
    private const string High = "post-1/video/media-1/mp4/video-high/1280x720.mp4";
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"video-variant-cleanup-v2-{Guid.NewGuid():N}"
    );

    [Fact]
    public async Task Preview_IsCompactAcrossVariantsInDifferentChunks()
    {
        string backupRoot = CreateBackup(
            (1, new[] { (Low, new byte[] { 1 }) }),
            (2, new[] { (High, new byte[] { 2, 3 }) })
        );

        VideoVariantChunkCleanupV2Manifest manifest =
            await new VideoVariantChunkCleanupV2Service().PreviewAsync(backupRoot, parallelism: 2);

        VideoVariantChunkCleanupV2Variant variant = Assert.Single(manifest.Variants);
        Assert.Equal(Low, manifest.Paths[variant.RemovePathIndex]);
        Assert.Equal(High, manifest.Paths[variant.KeepPathIndex]);
        Assert.Equal(2, manifest.Paths.Count);
        Assert.Single(manifest.Chunks.Single(chunk => chunk.Id == 1).RemovePathIndices);
        Assert.Empty(manifest.Chunks.Single(chunk => chunk.Id == 2).RemovePathIndices);
    }

    [Fact]
    public async Task SaveAndLoad_PreserveDeterministicHashWithoutMutatingManifest()
    {
        string backupRoot = CreateBackup(
            (1, new[] { (Low, new byte[] { 1 }), (High, new byte[] { 2 }) })
        );
        VideoVariantChunkCleanupV2Service sut = new();
        VideoVariantChunkCleanupV2Manifest manifest = await sut.PreviewAsync(backupRoot);
        string approval = manifest.ApprovalHash;
        string path = Path.Combine(_root, "preview.json");

        Assert.Equal(approval, sut.CalculateApprovalHash(manifest));
        Assert.Equal(approval, manifest.ApprovalHash);
        sut.SaveManifest(manifest, path);
        VideoVariantChunkCleanupV2Manifest loaded = sut.LoadManifest(path);

        Assert.Equal(approval, loaded.ApprovalHash);
        Assert.Equal(approval, sut.CalculateApprovalHash(loaded));
    }

    [Fact]
    public async Task Preflight_ValidatesCompleteBackup()
    {
        string backupRoot = CreateBackup(
            (1, new[] { (Low, new byte[] { 1 }), (High, new byte[] { 2, 3 }) })
        );
        VideoVariantChunkCleanupV2Service sut = new();
        VideoVariantChunkCleanupV2Manifest manifest = await sut.PreviewAsync(backupRoot);

        VideoVariantChunkCleanupV2PreflightResult result = await sut.PreflightAsync(manifest, 2);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(1, result.Chunks);
        Assert.Equal(2, result.Entries);
        Assert.Equal(1, result.Removals);
        Assert.Equal(1, result.Bytes);
    }

    [Fact]
    public async Task Apply_MissingRemovalFailsBeforeMutation()
    {
        string backupRoot = CreateBackup(
            (1, new[] { (Low, new byte[] { 1 }), (High, new byte[] { 2 }) })
        );
        VideoVariantChunkCleanupV2Service sut = new();
        VideoVariantChunkCleanupV2Manifest manifest = await sut.PreviewAsync(backupRoot);
        RewriteChunk(backupRoot, 1, [(High, new byte[] { 2 })]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ApplyAsync(manifest, manifest.ApprovalHash)
        );

        Assert.Equal(new[] { High }, ReadZipPaths(backupRoot, 1));
        Assert.False(File.Exists(Path.Combine(backupRoot, ".video-variant-cleanup.run.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preflight_RejectsDuplicateAndCaseCollision(bool exactDuplicate)
    {
        string other = exactDuplicate ? High : High.ToUpperInvariant();
        string backupRoot = CreateBackup(
            (
                1,
                new[]
                {
                    (Low, new byte[] { 1 }),
                    (High, new byte[] { 2 }),
                    (other, new byte[] { 2 }),
                }
            )
        );
        VideoVariantChunkCleanupV2Service sut = new();
        VideoVariantChunkCleanupV2Manifest manifest = await sut.PreviewAsync(backupRoot);

        VideoVariantChunkCleanupV2PreflightResult result = await sut.PreflightAsync(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error =>
                exactDuplicate
                    ? error.Contains("duplicate exact")
                    : error.Contains("OrdinalIgnoreCase")
        );
    }

    [Fact]
    public async Task Apply_RewritesZipAndJsonAndPreservesKeptMetadata()
    {
        string backupRoot = CreateBackup(
            (1, new[] { (Low, new byte[] { 1 }), (High, new byte[] { 2, 3 }) })
        );
        string zipPath = ChunkZip(backupRoot, 1);
        DateTimeOffset keptTime;
        int keptAttributes;

        using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Update))
        {
            ZipArchiveEntry kept = zip.GetEntry(High)!;
            kept.LastWriteTime = new DateTimeOffset(2024, 2, 4, 6, 8, 10, TimeSpan.Zero);
            kept.ExternalAttributes = 32;
            keptTime = kept.LastWriteTime;
            keptAttributes = kept.ExternalAttributes;
        }

        using (ZipArchive zip = ZipFile.OpenRead(zipPath))
        {
            ZipArchiveEntry kept = zip.GetEntry(High)!;
            keptTime = kept.LastWriteTime;
            keptAttributes = kept.ExternalAttributes;
        }

        RewriteFingerprintJsonSizes(backupRoot, 1);
        VideoVariantChunkCleanupV2Service sut = new();
        VideoVariantChunkCleanupV2Manifest manifest = await sut.PreviewAsync(backupRoot);
        VideoVariantChunkCleanupV2ApplyResult result = await sut.ApplyAsync(
            manifest,
            manifest.ApprovalHash
        );

        Assert.Equal(1, result.Removals);

        using (ZipArchive zip = ZipFile.OpenRead(zipPath))
        {
            Assert.Null(zip.GetEntry(Low));
            ZipArchiveEntry? keptEntry = zip.GetEntry(High);
            Assert.NotNull(keptEntry);
            ZipArchiveEntry kept = keptEntry;
            Assert.Equal(keptTime, kept.LastWriteTime);
            Assert.Equal(keptAttributes, kept.ExternalAttributes);
        }

        Chunk chunk = ReadChunk(backupRoot, 1);
        Assert.Equal([High], chunk.Data.Select(item => item.Path));
        Assert.False(File.Exists(Path.Combine(backupRoot, ".video-variant-cleanup.run.json")));
    }

    [Fact]
    public async Task Apply_ChangedFingerprintAbortsWithoutJournal()
    {
        string backupRoot = CreateBackup(
            (1, new[] { (Low, new byte[] { 1 }), (High, new byte[] { 2 }) })
        );
        VideoVariantChunkCleanupV2Service sut = new();
        VideoVariantChunkCleanupV2Manifest manifest = await sut.PreviewAsync(backupRoot);
        File.SetLastWriteTimeUtc(ChunkZip(backupRoot, 1), DateTime.UtcNow.AddMinutes(2));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ApplyAsync(manifest, manifest.ApprovalHash)
        );

        Assert.Equal(new[] { Low, High }, ReadZipPaths(backupRoot, 1));
        Assert.False(File.Exists(Path.Combine(backupRoot, ".video-variant-cleanup.run.json")));
    }

    [Fact]
    public async Task Apply_ResumesExistingPendingJournal()
    {
        string backupRoot = CreateBackup(
            (1, new[] { (Low, new byte[] { 1 }), (High, new byte[] { 2 }) })
        );
        VideoVariantChunkCleanupV2Service sut = new();
        VideoVariantChunkCleanupV2Manifest manifest = await sut.PreviewAsync(backupRoot);
        string journalPath = Path.Combine(backupRoot, ".video-variant-cleanup.run.json");
        File.WriteAllText(
            journalPath,
            JsonSerializer.Serialize(
                new VideoVariantChunkCleanupV2Journal
                {
                    ApprovalHash = manifest.ApprovalHash,
                    RunId = "test-run",
                    Chunks = [new VideoVariantChunkCleanupV2JournalChunk { Id = 1 }],
                }
            )
        );

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ApplyAsync(manifest, manifest.ApprovalHash, resume: false)
        );
        await sut.ApplyAsync(manifest, manifest.ApprovalHash, resume: true);

        Assert.Equal(new[] { High }, ReadZipPaths(backupRoot, 1));
        Assert.False(File.Exists(journalPath));
    }

    [Fact]
    public async Task Apply_SkipsRequestedChunkAndProcessesRemainingChunks()
    {
        const string medium = "post-1/video/media-1/mp4/video-medium/640x360.mp4";
        string backupRoot = CreateBackup(
            (1, new[] { (Low, new byte[] { 1 }) }),
            (2, new[] { (medium, new byte[] { 2 }) }),
            (3, new[] { (High, new byte[] { 3 }) })
        );
        VideoVariantChunkCleanupV2Service sut = new();
        VideoVariantChunkCleanupV2Manifest manifest = await sut.PreviewAsync(backupRoot);

        VideoVariantChunkCleanupV2ApplyResult result = await sut.ApplyAsync(
            manifest,
            manifest.ApprovalHash,
            skipChunkIds: new HashSet<int> { 1 }
        );

        Assert.Equal(1, result.SkippedChunks);
        Assert.Equal(1, result.Chunks);
        Assert.Equal(new[] { Low }, ReadZipPaths(backupRoot, 1));
        Assert.Empty(ReadZipPaths(backupRoot, 2));
        Assert.Equal(new[] { High }, ReadZipPaths(backupRoot, 3));
    }

    [Fact]
    public async Task RepairEntryFromEntry_RepairsTargetAndCorrectsJsonCrc()
    {
        byte[] sourceBytes = [1, 2, 3, 4, 5];
        string expectedHash = Convert.ToHexString(SHA256.HashData(sourceBytes));
        string backupRoot = CreateBackup(
            (1, new[] { (Low, sourceBytes), (High, Array.Empty<byte>()) })
        );
        Chunk before = ReadChunk(backupRoot, 1);
        ChunkData source = before.Data.Single(item => item.Path == Low);
        ChunkData target = before.Data.Single(item => item.Path == High);
        source.Hash = expectedHash;
        source.FileSize = sourceBytes.Length;
        target.Hash = expectedHash.ToLowerInvariant();
        target.FileSize = sourceBytes.Length;
        target.Crc32 = 0;
        File.WriteAllText(ChunkJson(backupRoot, 1), JsonSerializer.Serialize(before));

        VideoVariantChunkCleanupV2RepairResult result =
            await new VideoVariantChunkCleanupV2Service().RepairEntryFromEntryAsync(
                backupRoot,
                1,
                $"/{Low}",
                High.Replace('/', '\\'),
                expectedHash.ToLowerInvariant()
            );

        using ZipArchive zip = ZipFile.OpenRead(ChunkZip(backupRoot, 1));
        ZipArchiveEntry sourceEntry = zip.GetEntry(Low)!;
        ZipArchiveEntry targetEntry = zip.GetEntry(High)!;
        Assert.Equal(expectedHash, HashEntry(sourceEntry));
        Assert.Equal(expectedHash, HashEntry(targetEntry));
        Chunk after = ReadChunk(backupRoot, 1);
        ChunkData repaired = after.Data.Single(item => item.Path == High);
        Assert.Equal(targetEntry.Crc32, repaired.Crc32);
        Assert.Equal(targetEntry.Crc32, result.Crc32);
        Assert.Equal(expectedHash, repaired.Hash);
        Assert.Equal(sourceBytes.Length, repaired.FileSize);
        Assert.Equal([Low, High], zip.Entries.Select(entry => entry.FullName));
    }

    [Fact]
    public async Task RepairEntryFromEntry_WrongHashLeavesFilesUnchangedAndNoArtifacts()
    {
        byte[] sourceBytes = [1, 2, 3];
        string actualHash = Convert.ToHexString(SHA256.HashData(sourceBytes));
        string backupRoot = CreateBackup(
            (1, new[] { (Low, sourceBytes), (High, Array.Empty<byte>()) })
        );
        Chunk chunk = ReadChunk(backupRoot, 1);

        foreach (ChunkData item in chunk.Data)
        {
            item.Hash = new string('0', 64);
            item.FileSize = sourceBytes.Length;
        }

        File.WriteAllText(ChunkJson(backupRoot, 1), JsonSerializer.Serialize(chunk));
        byte[] zipBefore = File.ReadAllBytes(ChunkZip(backupRoot, 1));
        byte[] jsonBefore = File.ReadAllBytes(ChunkJson(backupRoot, 1));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new VideoVariantChunkCleanupV2Service().RepairEntryFromEntryAsync(
                    backupRoot,
                    1,
                    Low,
                    High,
                    new string('0', 64)
                )
        );

        Assert.NotEqual(new string('0', 64), actualHash);
        Assert.Equal(zipBefore, File.ReadAllBytes(ChunkZip(backupRoot, 1)));
        Assert.Equal(jsonBefore, File.ReadAllBytes(ChunkJson(backupRoot, 1)));
        Assert.Empty(Directory.GetFiles(Path.Combine(backupRoot, "chunks"), "*.repair.*"));
        Assert.Equal(new[] { Low, High }, ReadZipPaths(backupRoot, 1));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string CreateBackup(params (int Id, (string Path, byte[] Data)[] Entries)[] chunks)
    {
        string backupRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(backupRoot, "chunks"));

        foreach ((int id, (string Path, byte[] Data)[] entries) in chunks)
            RewriteChunk(backupRoot, id, entries);

        return backupRoot;
    }

    private static void RewriteChunk(
        string backupRoot,
        int id,
        IReadOnlyCollection<(string Path, byte[] Data)> entries
    )
    {
        string zipPath = ChunkZip(backupRoot, id);
        File.Delete(zipPath);

        using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach ((string path, byte[] data) in entries)
            {
                ZipArchiveEntry entry = zip.CreateEntry(path, CompressionLevel.NoCompression);
                using Stream stream = entry.Open();
                stream.Write(data);
            }
        }

        Chunk chunk = new()
        {
            Id = id,
            Data = entries
                .Select(entry => new ChunkData { Path = entry.Path, FileSize = entry.Data.Length })
                .ToList(),
        };
        File.WriteAllText(ChunkJson(backupRoot, id), JsonSerializer.Serialize(chunk));
    }

    private static void RewriteFingerprintJsonSizes(string backupRoot, int id)
    {
        Chunk chunk = ReadChunk(backupRoot, id);
        using ZipArchive zip = ZipFile.OpenRead(ChunkZip(backupRoot, id));

        foreach (ChunkData item in chunk.Data)
            item.FileSize = zip.GetEntry(item.Path)!.Length;

        File.WriteAllText(ChunkJson(backupRoot, id), JsonSerializer.Serialize(chunk));
    }

    private static Chunk ReadChunk(string backupRoot, int id) =>
        JsonSerializer.Deserialize<Chunk>(File.ReadAllText(ChunkJson(backupRoot, id)))!;

    private static string[] ReadZipPaths(string backupRoot, int id)
    {
        using ZipArchive zip = ZipFile.OpenRead(ChunkZip(backupRoot, id));
        return zip.Entries.Select(entry => entry.FullName).ToArray();
    }

    private static string HashEntry(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string ChunkZip(string backupRoot, int id) =>
        Path.Combine(backupRoot, "chunks", $"{id}.chunk.zip");

    private static string ChunkJson(string backupRoot, int id) =>
        Path.Combine(backupRoot, "chunks", $"{id}.chunk.json");
}
