using Backup.Infrastructure.Media.Data;
using Backup.Infrastructure.Media.Models;

namespace Backup.Tests;

public class SqliteMediaCachePersistenceIOServiceTests
{
    [Fact]
    public async Task SaveAndLoadPrimarySnapshot_RoundTripsEntries()
    {
        SqliteMediaCachePersistenceIOService sut = new();
        string root = CreateRoot();

        try
        {
            string file = Path.Combine(root, "cache", "media-cache.sqlite");
            List<MediaCacheEntry> entries =
            [
                CreateEntry("a.jpg", 10, 11, 1),
                CreateEntry("b.jpg", 20, 21, 2),
            ];

            await sut.SavePrimarySnapshot(file, entries);
            IReadOnlyList<MediaCacheEntry> loaded = await sut.LoadPrimarySnapshot(file);

            Assert.Equal(2, loaded.Count);
            AssertEntry(loaded[0], "a.jpg", 10, 11, 1);
            AssertEntry(loaded[1], "b.jpg", 20, 21, 2);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task SaveIncrementalSnapshot_UpsertsByFileName()
    {
        SqliteMediaCachePersistenceIOService sut = new();
        string root = CreateRoot();

        try
        {
            string directory = Path.Combine(root, "downloads");

            await sut.SaveIncrementalSnapshot(
                directory,
                CreateEntry("first.jpg", 10, 11, 1),
                "item.cache"
            );
            await sut.SaveIncrementalSnapshot(
                directory,
                CreateEntry("updated.jpg", 30, 31, 2),
                "item.cache"
            );

            IReadOnlyList<MediaCacheEntry> loaded = await sut.LoadIncrementalSnapshots(directory);

            Assert.Single(loaded);
            AssertEntry(loaded[0], "updated.jpg", 30, 31, 2);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static MediaCacheEntry CreateEntry(
        string path,
        long stream,
        long file,
        int partitionId
    ) =>
        new()
        {
            Path = path,
            Size = new MediaCacheSize { Stream = stream, File = file },
            PartitionId = partitionId,
        };

    private static void AssertEntry(
        MediaCacheEntry entry,
        string path,
        long stream,
        long file,
        int partitionId
    )
    {
        Assert.Equal(path, entry.Path);
        Assert.NotNull(entry.Size);
        Assert.Equal(stream, entry.Size!.Stream);
        Assert.Equal(file, entry.Size.File);
        Assert.Equal(partitionId, entry.PartitionId);
    }

    private static string CreateRoot()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "twitter-x-backup-tests",
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteDirectory(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
