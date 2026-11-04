using System.Text.Json;
using Backup.Infrastructure.Media.Maintenance;

namespace Backup.Tests.Media.Maintenance;

public sealed class VideoVariantLooseCleanupManifestSplitServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"manifest-split-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Split_RemapsPathsRetainsEveryOperationAndSignsBoundedChunks()
    {
        string sourcePath = WriteSource(BuildSource(5));
        string output = Path.Combine(_root, "output");

        VideoVariantLooseCleanupSplitIndex index = new VideoVariantLooseCleanupManifestSplitService().Split(sourcePath, output, 2);

        Assert.Equal(3, index.Total);
        Assert.Equal(5, index.Operations);
        Assert.Equal(236, index.Bytes);
        Assert.Equal(5, index.Chunks.Sum(value => value.Operations));
        Assert.All(index.Chunks, value => Assert.InRange(value.Operations, 1, 2));
        Assert.Equal(BuildSource(5).ApprovalHash, index.SourceApprovalHash);
        Assert.True(File.Exists(Path.Combine(output, "index.json")));
        Assert.Empty(Directory.EnumerateFiles(output, "*.partial"));

        VideoVariantLooseCleanupPreviewService loader = new();
        List<(string Remove, string Keep, int Partition, int? KeepPartition, long Size, long Ticks, bool Direct, long DirectSize, long DirectTicks)> actual = [];
        foreach (VideoVariantLooseCleanupSplitChunk chunk in index.Chunks)
        {
            VideoVariantLooseCleanupManifest manifest = loader.LoadManifest(Path.Combine(output, chunk.Manifest));
            Assert.Equal(chunk.ApprovalHash, manifest.ApprovalHash);
            Assert.True(manifest.PhysicalChecked);
            Assert.Null(manifest.SelectionLimit);
            Assert.Equal(1, manifest.FileParallelism);
            Assert.Empty(manifest.Issues);
            Assert.Equal("store", manifest.MediaStoreId);
            Assert.Equal(1234, manifest.CacheRowsScanned);
            actual.AddRange(manifest.Operations.Select(operation => (
                manifest.Paths[operation.RemovePathIndex],
                manifest.Paths[operation.KeepPathIndex],
                operation.PartitionId,
                operation.KeepPartitionId,
                operation.OriginalSize,
                operation.OriginalLastWriteUtcTicks,
                operation.DirectPresent,
                operation.DirectSize,
                operation.DirectLastWriteUtcTicks
            )));
        }

        Assert.Equal(ExpectedOperations(5), actual);
    }

    [Fact]
    public void Split_CreatesMissingOutputDirectory()
    {
        string output = Path.Combine(_root, "missing", "chunks");
        new VideoVariantLooseCleanupManifestSplitService().Split(WriteSource(BuildSource(1)), output, 1);
        Assert.True(Directory.Exists(output));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("physical")]
    [InlineData("selection")]
    [InlineData("fatal-issue")]
    [InlineData("touched-invalid-group")]
    [InlineData("bad-index")]
    [InlineData("bad-hash")]
    public void Split_RejectsMalformedOrFatalSource(string kind)
    {
        VideoVariantLooseCleanupManifest source = BuildSource(1);
        source = kind switch
        {
            "version" => source with { Version = 2 },
            "physical" => source with { PhysicalChecked = false },
            "selection" => source with { SelectionLimit = 1 },
            "fatal-issue" => source with { Issues = [new("unrelated", "file-missing", "fatal")] },
            "touched-invalid-group" => source with { Issues = [new("group/0", "keep-set-invalid", "invalid")] },
            "bad-index" => source with
            {
                Operations = [new VideoVariantLooseCleanupOperation { RemovePathIndex = 99, KeepPathIndex = 0, PartitionId = 1 }],
            },
            _ => source,
        };
        source.ApprovalHash = VideoVariantLooseCleanupPreviewService.CalculateApprovalHash(source);
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, $"malformed-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(source, new JsonSerializerOptions { WriteIndented = true }));
        if (kind == "bad-hash")
        {
            string json = File.ReadAllText(path);
            File.WriteAllText(path, json.Replace(source.ApprovalHash, new string('0', 64), StringComparison.Ordinal));
        }

        Assert.Throws<InvalidOperationException>(() =>
            new VideoVariantLooseCleanupManifestSplitService().Split(path, Path.Combine(_root, "rejected-" + kind), 1)
        );
    }

    private string WriteSource(VideoVariantLooseCleanupManifest source)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, $"source-{Guid.NewGuid():N}.json");
        new VideoVariantLooseCleanupPreviewService().SaveManifest(source, path);
        return path;
    }

    private static VideoVariantLooseCleanupManifest BuildSource(int count)
    {
        List<string> paths = Enumerable.Range(0, count)
            .SelectMany(index => new[] { $"group/{index}/keep.mp4", $"group/{index}/remove.mp4" })
            .Order(StringComparer.Ordinal)
            .ToList();
        Dictionary<string, int> indices = paths.Select((path, index) => (path, index)).ToDictionary(value => value.path, value => value.index);
        VideoVariantLooseCleanupManifest source = new()
        {
            Version = 1,
            CreatedUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            DataConfigPath = Path.GetFullPath("Data.json"),
            DataConfigLength = 42,
            DataConfigLastWriteUtcTicks = 99,
            MediaStoreId = "store",
            CacheId = "cache",
            CacheNamespace = "namespace",
            DirectRoot = Path.GetFullPath("direct"),
            CacheRowsScanned = 1234,
            VideoEntries = 567,
            FileParallelism = 8,
            SelectionLimit = null,
            PhysicalChecked = true,
            Paths = paths,
            Operations = Enumerable.Range(0, count).Select(index => new VideoVariantLooseCleanupOperation
            {
                RemovePathIndex = indices[$"group/{index}/remove.mp4"],
                KeepPathIndex = indices[$"group/{index}/keep.mp4"],
                PartitionId = index + 1,
                KeepPartitionId = index + 10,
                OriginalSize = index + 20,
                OriginalLastWriteUtcTicks = index + 30,
                DirectPresent = index % 2 == 0,
                DirectSize = index + 40,
                DirectLastWriteUtcTicks = index + 50,
            }).ToList(),
            Issues = [new("invalid/unrelated", "keep-set-invalid", "Excluded group has no operations.")],
        };
        source.ApprovalHash = VideoVariantLooseCleanupPreviewService.CalculateApprovalHash(source);
        return source;
    }

    private static List<(string, string, int, int?, long, long, bool, long, long)> ExpectedOperations(int count) =>
        Enumerable.Range(0, count)
            .Select(index => (
                $"group/{index}/remove.mp4",
                $"group/{index}/keep.mp4",
                index + 1,
                (int?)(index + 10),
                (long)(index + 20),
                (long)(index + 30),
                index % 2 == 0,
                (long)(index + 40),
                (long)(index + 50)
            ))
            .ToList();

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
