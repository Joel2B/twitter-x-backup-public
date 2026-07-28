using Backup.Infrastructure.Media.Data;
using Backup.Infrastructure.Media.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.Tests;

public class SqliteMediaCachePersistenceIOServiceTests
{
    [Fact]
    public async Task SaveAndLoadPrimarySnapshot_RoundTripsEntries()
    {
        SqliteMediaCachePersistenceIOService sut = new(
            NullLogger<SqliteMediaCachePersistenceIOService>.Instance
        );
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
        SqliteMediaCachePersistenceIOService sut = new(
            NullLogger<SqliteMediaCachePersistenceIOService>.Instance
        );
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

    [Fact]
    public async Task ApplyRecheckChanges_UpdatesOnlyChangedEntries()
    {
        SqliteMediaCachePersistenceIOService sut = new(
            NullLogger<SqliteMediaCachePersistenceIOService>.Instance
        );
        string root = CreateRoot();

        try
        {
            string file = Path.Combine(root, "cache", "media-cache.sqlite");
            string incrementalDirectory = Path.Combine(root, "incremental");
            await sut.SavePrimarySnapshot(
                file,
                [
                    CreateEntry("updated.jpg", 10, 11, 1),
                    CreateEntry("removed.jpg", 20, 21, 2),
                    CreateEntry("unchanged.jpg", 30, 31, 3),
                ]
            );
            await sut.SaveIncrementalSnapshot(
                incrementalDirectory,
                CreateEntry("updated.jpg", 10, 11, 1),
                "updated.cache"
            );
            await sut.SaveIncrementalSnapshot(
                incrementalDirectory,
                CreateEntry("unrelated.jpg", 40, 41, 4),
                "unrelated.cache"
            );

            await sut.ApplyRecheckChanges(
                file,
                incrementalDirectory,
                finalEntries: [],
                upserts: [CreateEntry("updated.jpg", 100, 101, 8)],
                removals: ["removed.jpg"]
            );

            IReadOnlyList<MediaCacheEntry> primary = await sut.LoadPrimarySnapshot(file);
            IReadOnlyList<MediaCacheEntry> incremental = await sut.LoadIncrementalSnapshots(
                incrementalDirectory
            );

            Assert.Equal(2, primary.Count);
            AssertEntry(primary[0], "unchanged.jpg", 30, 31, 3);
            AssertEntry(primary[1], "updated.jpg", 100, 101, 8);
            Assert.Single(incremental);
            AssertEntry(incremental[0], "unrelated.jpg", 40, 41, 4);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Writes_TreatPathsCaseInsensitively()
    {
        SqliteMediaCachePersistenceIOService sut = new(
            NullLogger<SqliteMediaCachePersistenceIOService>.Instance
        );
        string root = CreateRoot();

        try
        {
            string file = Path.Combine(root, "cache.sqlite");
            string incrementalDirectory = Path.Combine(root, "incremental");

            await sut.SavePrimarySnapshot(
                file,
                [CreateEntry("Media/ITEM.jpg", 10, 11, 1), CreateEntry("media/item.JPG", 20, 21, 2)]
            );
            await sut.SaveIncrementalSnapshot(
                incrementalDirectory,
                CreateEntry("Media/ITEM.jpg", 10, 11, 1),
                "first.cache"
            );
            await sut.SaveIncrementalSnapshot(
                incrementalDirectory,
                CreateEntry("media/item.JPG", 20, 21, 2),
                "second.cache"
            );

            IReadOnlyList<MediaCacheEntry> primary = await sut.LoadPrimarySnapshot(file);
            IReadOnlyList<MediaCacheEntry> incremental = await sut.LoadIncrementalSnapshots(
                incrementalDirectory
            );

            Assert.Single(primary);
            AssertEntry(primary[0], "media/item.JPG", 20, 21, 2);
            Assert.Single(incremental);
            AssertEntry(incremental[0], "media/item.JPG", 20, 21, 2);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task IncrementalWritesAndRecheck_CanRunConcurrently()
    {
        SqliteMediaCachePersistenceIOService sut = new(
            NullLogger<SqliteMediaCachePersistenceIOService>.Instance
        );
        string root = CreateRoot();

        try
        {
            string file = Path.Combine(root, "cache.sqlite");
            string incrementalDirectory = Path.Combine(root, "incremental");
            await sut.SavePrimarySnapshot(file, [CreateEntry("item.jpg", 10, 11, 1)]);

            Task[] writes = Enumerable
                .Range(0, 20)
                .Select(index =>
                    sut.SaveIncrementalSnapshot(
                        incrementalDirectory,
                        CreateEntry($"new-{index}.jpg", index, index, 1),
                        $"{index}.cache"
                    )
                )
                .ToArray();
            Task recheck = sut.ApplyRecheckChanges(
                file,
                incrementalDirectory,
                finalEntries: [],
                upserts: [CreateEntry("ITEM.jpg", 20, 21, 2)],
                removals: []
            );

            await Task.WhenAll([.. writes, recheck]);

            IReadOnlyList<MediaCacheEntry> primary = await sut.LoadPrimarySnapshot(file);
            Assert.Single(primary);
            AssertEntry(primary[0], "ITEM.jpg", 20, 21, 2);
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
