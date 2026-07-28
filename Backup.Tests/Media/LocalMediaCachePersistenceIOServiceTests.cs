using Backup.Application.Media.Maintenance;
using Backup.Infrastructure.Media.Data;
using Backup.Infrastructure.Media.Models;

namespace Backup.Tests;

public sealed class LocalMediaCachePersistenceIOServiceTests
{
    [Fact]
    public async Task LoadIncrementalSnapshots_LoadsLegacyCacheSidecars()
    {
        LocalMediaCachePersistenceIOService sut = new(new MediaCacheJsonSnapshotService());
        string root = CreateRoot();

        try
        {
            await sut.SaveIncrementalSnapshot(
                root,
                new MediaCacheEntry
                {
                    Path = "legacy.jpg",
                    Size = new MediaCacheSize { Stream = 10, File = 11 },
                    PartitionId = 2,
                },
                "legacy.cache"
            );

            IReadOnlyList<MediaCacheEntry> loaded = await sut.LoadIncrementalSnapshots(root);

            MediaCacheEntry entry = Assert.Single(loaded);
            Assert.Equal("legacy.jpg", entry.Path);
            Assert.Equal(10, entry.Size?.Stream);
            Assert.Equal(11, entry.Size?.File);
            Assert.Equal(2, entry.PartitionId);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task SaveIncrementalSnapshot_ConcurrentWritesRemainReadable()
    {
        LocalMediaCachePersistenceIOService sut = new(new MediaCacheJsonSnapshotService());
        string root = CreateRoot();

        try
        {
            Task[] writes = Enumerable
                .Range(0, 20)
                .Select(index =>
                    sut.SaveIncrementalSnapshot(
                        root,
                        new MediaCacheEntry
                        {
                            Path = $"item-{index}.jpg",
                            Size = new MediaCacheSize { Stream = index, File = index },
                            PartitionId = 1,
                        },
                        $"{index}.cache"
                    )
                )
                .ToArray();

            await Task.WhenAll(writes);

            IReadOnlyList<MediaCacheEntry> loaded = await sut.LoadIncrementalSnapshots(root);
            Assert.Equal(20, loaded.Count);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot() =>
        Path.Combine(Path.GetTempPath(), "twitter-x-backup-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
