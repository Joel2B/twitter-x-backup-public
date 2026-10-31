using System.IO.Compression;
using System.Text.Json;
using Backup.Infrastructure.Media.Maintenance;
using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Tests;

public sealed class VideoVariantCleanupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"video-variant-cleanup-{Guid.NewGuid():N}"
    );

    [Fact]
    public void PreviewAndApply_RemovesLowerResolutionFromMediaDirectAndChunk()
    {
        string mediaRoot = Path.Combine(_root, "media");
        string backupRoot = Path.Combine(_root, "backup");
        string directRoot = Path.Combine(backupRoot, "direct");
        string chunksRoot = Path.Combine(backupRoot, "chunks");
        const string low = "post-1/video/media-1/mp4/video-low/320x180.mp4";
        const string high = "post-1/video/media-1/mp4/video-high/1280x720.mp4";

        WriteFile(mediaRoot, low, [1, 2, 3]);
        WriteFile(mediaRoot, high, [1, 2, 3, 4]);
        WriteFile(directRoot, low, [1, 2, 3]);
        Directory.CreateDirectory(chunksRoot);

        string zipPath = Path.Combine(chunksRoot, "7.chunk.zip");

        using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteEntry(zip, low, [1, 2, 3]);
            WriteEntry(zip, high, [1, 2, 3, 4]);
        }

        string chunkManifestPath = Path.Combine(chunksRoot, "7.chunk.json");
        File.WriteAllText(
            chunkManifestPath,
            JsonSerializer.Serialize(
                new Chunk
                {
                    Id = 7,
                    Data =
                    [
                        new ChunkData { Path = low, FileSize = 3 },
                        new ChunkData { Path = high, FileSize = 4 },
                    ],
                }
            )
        );

        VideoVariantCleanupService sut = new();
        VideoVariantCleanupManifest preview = sut.Preview([mediaRoot], backupRoot);

        VideoVariantCleanupCandidate candidate = Assert.Single(preview.Candidates);
        Assert.Equal("1280x720", candidate.KeepResolution);
        Assert.Equal("320x180", candidate.RemoveResolution);
        Assert.Equal(3, candidate.Copies.Count);
        Assert.Contains(candidate.Copies, copy => copy.Kind == "media");
        Assert.Contains(candidate.Copies, copy => copy.Kind == "backup-direct");
        Assert.Contains(candidate.Copies, copy => copy.Kind == "backup-chunk");

        string approval = preview.ApprovalHash;
        string manifestPath = Path.Combine(_root, "preview.json");
        sut.SaveManifest(preview, manifestPath);
        VideoVariantCleanupManifest loaded = sut.LoadManifest(manifestPath);
        Assert.Equal(approval, loaded.ApprovalHash);

        VideoVariantCleanupResult result = sut.Apply(loaded);

        Assert.Equal(1, result.RemovedVariants);
        Assert.Equal(3, result.RemovedCopies);
        Assert.False(File.Exists(Path.Combine(mediaRoot, ToPlatformPath(low))));
        Assert.True(File.Exists(Path.Combine(mediaRoot, ToPlatformPath(high))));
        Assert.False(File.Exists(Path.Combine(directRoot, ToPlatformPath(low))));

        using (ZipArchive zip = ZipFile.OpenRead(zipPath))
        {
            Assert.Null(zip.GetEntry(low));
            Assert.NotNull(zip.GetEntry(high));
        }

        Chunk chunk = JsonSerializer.Deserialize<Chunk>(File.ReadAllText(chunkManifestPath))!;
        Assert.DoesNotContain(chunk.Data, data => data.Path == low);
        Assert.Contains(chunk.Data, data => data.Path == high);
    }

    [Fact]
    public void Apply_RejectsChangedCopyBeforeDeletingAnything()
    {
        string mediaRoot = Path.Combine(_root, "media");
        string backupRoot = Path.Combine(_root, "backup");
        const string low = "post-1/video/media-1/mp4/video-1/320x180.mp4";
        const string high = "post-1/video/media-1/mp4/video-1/1280x720.mp4";
        WriteFile(mediaRoot, low, [1]);
        WriteFile(mediaRoot, high, [1, 2]);

        VideoVariantCleanupService sut = new();
        VideoVariantCleanupManifest manifest = sut.Preview([mediaRoot], backupRoot);
        File.WriteAllBytes(Path.Combine(mediaRoot, ToPlatformPath(low)), [9, 9, 9]);

        Assert.Throws<InvalidOperationException>(() => sut.Apply(manifest));
        Assert.True(File.Exists(Path.Combine(mediaRoot, ToPlatformPath(low))));
        Assert.True(File.Exists(Path.Combine(mediaRoot, ToPlatformPath(high))));
    }

    [Fact]
    public void Preview_DoesNotRemoveNonDominatedResolutions()
    {
        string mediaRoot = Path.Combine(_root, "media");
        string backupRoot = Path.Combine(_root, "backup");
        WriteFile(mediaRoot, "post-1/video/media-1/mp4/video-1/1920x800.mp4", [1]);
        WriteFile(mediaRoot, "post-1/video/media-1/mp4/video-1/1440x1080.mp4", [2]);

        VideoVariantCleanupManifest manifest = new VideoVariantCleanupService().Preview(
            [mediaRoot],
            backupRoot
        );

        Assert.Empty(manifest.Candidates);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static void WriteFile(string root, string relativePath, byte[] content)
    {
        string path = Path.Combine(root, ToPlatformPath(relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private static void WriteEntry(ZipArchive zip, string path, byte[] content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(path, CompressionLevel.NoCompression);
        using Stream stream = entry.Open();
        stream.Write(content);
    }

    private static string ToPlatformPath(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar);
}
