using Backup.Infrastructure.Media.Data;
using Backup.Infrastructure.Media.Models;
using Microsoft.Data.Sqlite;
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

    [Fact]
    public async Task ApplyRecheckChanges_HandlesCaseInsensitiveBatch()
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
                [
                    CreateEntry("Media/Keep.jpg", 1, 1, 1),
                    CreateEntry("Media/Update.jpg", 2, 2, 2),
                    CreateEntry("Media/Remove.jpg", 3, 3, 3),
                ]
            );
            await sut.SaveIncrementalSnapshot(
                incrementalDirectory,
                CreateEntry("MEDIA/UPDATE.JPG", 2, 2, 2),
                "update.cache"
            );
            await sut.SaveIncrementalSnapshot(
                incrementalDirectory,
                CreateEntry("media/remove.JPG", 3, 3, 3),
                "remove.cache"
            );
            await sut.SaveIncrementalSnapshot(
                incrementalDirectory,
                CreateEntry("unrelated.jpg", 4, 4, 4),
                "unrelated.cache"
            );

            await sut.ApplyRecheckChanges(
                file,
                incrementalDirectory,
                finalEntries: [],
                upserts: [CreateEntry("media/UPDATE.JPG", 20, 21, 8)],
                removals: ["MEDIA/REMOVE.JPG"]
            );

            IReadOnlyList<MediaCacheEntry> primary = await sut.LoadPrimarySnapshot(file);
            IReadOnlyList<MediaCacheEntry> incremental = await sut.LoadIncrementalSnapshots(
                incrementalDirectory
            );
            Assert.Equal(2, primary.Count);
            Assert.Contains(primary, entry => entry.Path == "Media/Keep.jpg");
            Assert.Contains(
                primary,
                entry => entry.Path == "media/UPDATE.JPG" && entry.Size?.File == 21
            );
            Assert.Single(incremental);
            Assert.Equal("unrelated.jpg", incremental[0].Path);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ApplyRecheckChanges_RemovalWinsOverSamePathUpsert()
    {
        SqliteMediaCachePersistenceIOService sut = new(
            NullLogger<SqliteMediaCachePersistenceIOService>.Instance
        );
        string root = CreateRoot();

        try
        {
            string file = Path.Combine(root, "cache.sqlite");
            string incrementalDirectory = Path.Combine(root, "incremental");
            await sut.SavePrimarySnapshot(file, [CreateEntry("item.jpg", 1, 1, 1)]);

            await sut.ApplyRecheckChanges(
                file,
                incrementalDirectory,
                finalEntries: [],
                upserts: [CreateEntry("ITEM.jpg", 2, 2, 2)],
                removals: ["item.JPG"]
            );

            Assert.Empty(await sut.LoadPrimarySnapshot(file));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ApplyRecheckChanges_RollsBackPrimaryWhenIncrementalDeleteFails()
    {
        SqliteMediaCachePersistenceIOService sut = new(
            NullLogger<SqliteMediaCachePersistenceIOService>.Instance
        );
        string root = CreateRoot();

        try
        {
            string file = Path.Combine(root, "cache.sqlite");
            string incrementalDirectory = Path.Combine(root, "incremental");
            await sut.SavePrimarySnapshot(file, [CreateEntry("item.jpg", 1, 1, 1)]);
            await sut.SaveIncrementalSnapshot(
                incrementalDirectory,
                CreateEntry("item.jpg", 1, 1, 1),
                "item.cache"
            );

            string incrementalFile = Path.Combine(
                incrementalDirectory,
                "media-cache-incremental.sqlite"
            );

            await using (
                SqliteConnection connection = new($"Data Source={incrementalFile};Pooling=False")
            )
            {
                await connection.OpenAsync();
                await using SqliteCommand trigger = connection.CreateCommand();
                trigger.CommandText = """
                    CREATE TRIGGER fail_recheck_delete
                    BEFORE DELETE ON media_cache_incremental_entries
                    BEGIN
                        SELECT RAISE(ABORT, 'forced incremental failure');
                    END;
                    """;
                await trigger.ExecuteNonQueryAsync();
            }

            await Assert.ThrowsAnyAsync<SqliteException>(
                () =>
                    sut.ApplyRecheckChanges(
                        file,
                        incrementalDirectory,
                        finalEntries: [],
                        upserts: [CreateEntry("ITEM.jpg", 2, 2, 2)],
                        removals: []
                    )
            );

            MediaCacheEntry primary = Assert.Single(await sut.LoadPrimarySnapshot(file));
            AssertEntry(primary, "item.jpg", 1, 1, 1);
            Assert.Single(await sut.LoadIncrementalSnapshots(incrementalDirectory));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ApplyRecheckChanges_HandlesLargeDelta()
    {
        SqliteMediaCachePersistenceIOService sut = new(
            NullLogger<SqliteMediaCachePersistenceIOService>.Instance
        );
        string root = CreateRoot();

        try
        {
            string file = Path.Combine(root, "cache.sqlite");
            string incrementalDirectory = Path.Combine(root, "incremental");
            List<MediaCacheEntry> entries = Enumerable
                .Range(0, 5000)
                .Select(index => CreateEntry($"item-{index}.jpg", index, index, 1))
                .ToList();
            List<MediaCacheEntry> updates = entries
                .Take(2467)
                .Select(entry => CreateEntry(entry.Path.ToUpperInvariant(), 10_000, 10_001, 2))
                .ToList();
            await sut.SavePrimarySnapshot(file, entries);

            await sut.ApplyRecheckChanges(
                file,
                incrementalDirectory,
                finalEntries: [],
                upserts: updates,
                removals: []
            );

            IReadOnlyList<MediaCacheEntry> loaded = await sut.LoadPrimarySnapshot(file);
            Assert.Equal(5000, loaded.Count);
            Assert.Equal(2467, loaded.Count(entry => entry.Size?.File == 10_001));
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
