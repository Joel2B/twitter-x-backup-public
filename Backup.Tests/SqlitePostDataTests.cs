using Backup.Infrastructure.Posts.Data.Sqlite;
using Backup.Application.Posts;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Posts;
using Backup.Infrastructure.Models.Config.Downloads;
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.Tests;

public class SqlitePostDataTests
{
    [Fact]
    public async Task GetHashesById_UsesSyncedMeta_AfterInsertAndUpdate()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();

            Post post = CreatePost("p1", "profile-1", "hello", "user-1", "posts");
            await sut.AddPosts("user-1", "posts", [post]);
            await sut.Save();

            Dictionary<string, string> firstHashes = await sut.GetHashesById();
            Assert.True(firstHashes.ContainsKey("p1"));

            Post updated = CreatePost("p1", "profile-1", "hello-updated", "user-1", "posts");

            await sut.AddPosts("user-1", "posts", [updated]);
            await sut.Save();

            Dictionary<string, string> secondHashes = await sut.GetHashesById();
            Assert.True(secondHashes.ContainsKey("p1"));
            Assert.NotEqual(firstHashes["p1"], secondHashes["p1"]);
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task MarkDeletedExcept_OnlyMarksScopeIds()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();

            List<Post> posts =
            [
                CreatePost("p1", "profile-1", "a", "user-1", "posts"),
                CreatePost("p2", "profile-1", "b", "user-1", "posts"),
                CreatePost("p3", "profile-1", "c", "user-1", "likes"),
            ];

            await sut.UpsertPosts(posts);
            await sut.Save();

            int marked = await sut.MarkDeletedExcept("user-1", "posts", ["p1"]);
            Assert.Equal(1, marked);
            await sut.Save();

            List<Post> all = await sut.GetByIds(["p1", "p2", "p3"]);
            Dictionary<string, Post> byId = all.ToDictionary(post => post.Id);

            Assert.False(byId["p1"].Deleted);
            Assert.True(byId["p2"].Deleted);
            Assert.False(byId["p3"].Deleted);
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Reset_KeepsLastDuplicateAndSyncsHashMeta()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();

            List<Post> resetPosts =
            [
                CreatePost("p1", "profile-1", "first", "user-1", "posts"),
                CreatePost("p1", "profile-1", "second", "user-1", "posts"),
            ];

            await sut.Reset(resetPosts);
            await sut.Save();

            Assert.Equal(1, await sut.GetCount());
            Post stored = (await sut.GetByIds(["p1"])).Single();
            Assert.Equal("second", stored.Description);

            Dictionary<string, string> hashes = await sut.GetHashesById();
            Assert.Single(hashes);
            Assert.True(hashes.ContainsKey("p1"));
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task GetByIds_SupportsLargeBatchesWithChunking()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();

            const int total = 1200;

            List<Post> posts = Enumerable
                .Range(1, total)
                .Select(i =>
                    CreatePost(
                        $"p{i}",
                        $"profile-{i % 10}",
                        $"post-{i}",
                        "user-1",
                        "posts",
                        createdAt: $"2025-01-01T00:00:{i % 60:00}Z"
                    )
                )
                .ToList();

            await sut.Reset(posts);
            await sut.Save();

            List<string> ids = posts.Select(post => post.Id).ToList();
            List<Post> loaded = await sut.GetByIds(ids);

            Assert.Equal(total, loaded.Count);
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task UpsertPosts_DuplicateIdsInSameBatch_KeepsLastVersion()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();

            await sut.UpsertPosts(
            [
                CreatePost("p1", "profile-1", "first", "user-1", "posts"),
                CreatePost("p1", "profile-1", "second", "user-1", "posts"),
            ]);
            await sut.Save();

            Assert.Equal(1, await sut.GetCount());

            Post stored = (await sut.GetByIds(["p1"])).Single();
            Assert.Equal("second", stored.Description);
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task MarkDeletedExcept_IsIdempotent_WhenAlreadyDeleted()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();

            await sut.UpsertPosts(
            [
                CreatePost("p1", "profile-1", "one", "user-1", "posts"),
                CreatePost("p2", "profile-1", "two", "user-1", "posts"),
            ]);
            await sut.Save();

            int firstMarked = await sut.MarkDeletedExcept("user-1", "posts", ["p1"]);
            await sut.Save();

            int secondMarked = await sut.MarkDeletedExcept("user-1", "posts", ["p1"]);
            await sut.Save();

            Assert.Equal(1, firstMarked);
            Assert.Equal(0, secondMarked);

            PostStoreCounts counts = await sut.GetStoreCounts();
            Assert.Equal(1, counts.Changes);
            Assert.Equal(1, counts.ChangeFields);
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task AddPosts_SamePayload_DoesNotCreateChange()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();

            Post post = CreatePost("p1", "profile-1", "same", "user-1", "posts");

            await sut.AddPosts("user-1", "posts", [post]);
            await sut.Save();

            await sut.AddPosts("user-1", "posts", [post.Clone()]);
            await sut.Save();

            Post stored = (await sut.GetByIds(["p1"])).Single();
            Assert.Empty(stored.Changes);
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    private static (SqlitePostData Sut, string Root) CreateSut()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "backup-sqlite-tests",
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(root);

        PartitionConfig primary = new()
        {
            Id = 1,
            Type = "primary",
            Size = 100,
            UsableSpace = 80,
            Paths = [root],
            Enabled = true,
        };

        TestPartition partition = new([primary]);

        StoragePost storage = new()
        {
            Id = "sqlite-test",
            Type = "sqlite",
            Enabled = true,
            Partitions = [1],
            Tasks = new Tasks
            {
                Prune = false,
                Verify = false,
                Fix = false,
            },
            Paths = new Paths
            {
                Paths = ["data"],
                Post = new PathConfig { Paths = ["post"], File = "posts.db" },
            },
        };

        SqlitePostData sut = new(
            NullLogger<SqlitePostData>.Instance,
            storage,
            partition,
            new PostMergeService(),
            new PostSoftDeleteSelectionService()
        );
        return (sut, root);
    }

    private static Post CreatePost(
        string id,
        string profileId,
        string description,
        string userId,
        string origin,
        string? createdAt = null,
        string? previous = "prev",
        string? next = "next"
    ) =>
        new()
        {
            Id = id,
            Profile = new() { Id = profileId, UserName = profileId },
            Description = description,
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = createdAt ?? "2025-01-01T00:00:00Z",
            Hashtags = ["tag1"],
            Medias =
            [
                new()
                {
                    Id = $"m-{id}",
                    Url = $"https://cdn.local/{id}.jpg",
                    Type = "photo",
                },
            ],
            Deleted = false,
            Index = new Dictionary<string, Dictionary<string, IndexData>>(StringComparer.Ordinal)
            {
                [userId] = new Dictionary<string, IndexData>(StringComparer.Ordinal)
                {
                    [origin] = new() { Previous = previous, Next = next },
                },
            },
            Changes = [],
        };

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        const int maxAttempts = 20;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException)
            {
                if (attempt == maxAttempts)
                    return;

                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == maxAttempts)
                    return;

                Thread.Sleep(100);
            }
        }
    }

    private sealed class TestPartition(List<PartitionConfig> partitions) : IPartition
    {
        private readonly List<PartitionConfig> _partitions = partitions;

        public List<PartitionConfig> GetPartitions(List<int>? ids = null)
        {
            if (ids is null)
                return [.. _partitions];

            HashSet<int> filter = [.. ids];
            return [.. _partitions.Where(partition => filter.Contains(partition.Id))];
        }

        public PartitionConfig GetPath(int? id = null, long size = 0)
        {
            if (id is null)
                return GetPrimary();

            return _partitions.First(partition => partition.Id == id);
        }

        public List<PartitionConfig> GetCache() => [];

        public PartitionConfig GetPrimary() =>
            _partitions.First(partition => partition.Type == "primary");

        public PartitionConfig GetHeavy() => GetPrimary();

        public void SetupSizes(Dictionary<int, long> sizes) { }
    }
}
