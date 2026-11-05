using Backup.Application.Posts;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Posts;
using Backup.Infrastructure.Models.Config.Downloads;
using Backup.Infrastructure.Posts.Data.Sqlite;
using Backup.Infrastructure.Posts.Models.Stored;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.Tests;

public class SqlitePostDataTests
{
    [Fact]
    public async Task GetMediaInputs_ProjectsProfilesPhotosAndVideoVariantsFromRelationalTables()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();
            Post photo = CreatePost("photo-post", "photo-profile", "photo", "user-1", "likes");
            photo.Profile.ImageUrl = "https://pbs.twimg.com/profile_images/1/avatar_normal.jpg";
            photo.Medias =
            [
                new PostMedia
                {
                    Id = "photo-media",
                    Type = "photo",
                    Url = "https://pbs.twimg.com/media/photo.jpg",
                },
            ];
            Post video = CreatePost("video-post", "video-profile", "video", "user-1", "bookmarks");
            video.Medias =
            [
                new PostMedia
                {
                    Id = "video-media",
                    Type = "video",
                    Url = "https://pbs.twimg.com/amplify_video_thumb/video.jpg",
                    VideoInfo = new PostVideoInfo
                    {
                        Variants =
                        [
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Bitrate = 2176000,
                                Url = "https://video.twimg.com/vid/1280x720/video.mp4",
                            },
                        ],
                    },
                },
            ];
            await sut.UpsertPosts([photo, video]);
            await sut.Save();

            List<MediaInput> inputs = (await sut.GetMediaInputs())!;
            List<PostMedia> medias = inputs.SelectMany(input => input.Medias ?? []).ToList();

            Assert.Contains(inputs, input => input.Profile.Id == "photo-profile");
            Assert.Contains(medias, media => media.Id == "photo-media" && media.Type == "photo");
            PostMedia videoMedia = Assert.Single(medias, media => media.Id == "video-media");
            PostVariant variant = Assert.Single(videoMedia.VideoInfo!.Variants!);
            Assert.Equal("video/mp4", variant.ContentType);
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task DeletePosts_RemovesOnlyOrphanProfiles()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();
            await sut.UpsertPosts(
                [
                    CreatePost("p1", "shared", "one", "user-1", "posts"),
                    CreatePost("p2", "shared", "two", "user-1", "posts"),
                    CreatePost("p3", "orphan", "three", "user-1", "posts"),
                ]
            );
            await sut.Save();

            await sut.DeletePosts(["p1", "p3"]);

            PostStoreCounts counts = await sut.GetStoreCounts();
            Assert.Equal(1, counts.Posts);
            Assert.Equal(1, counts.Profiles);
            Assert.Equal("p2", (await sut.GetByIds(["p1", "p2", "p3"])).Single().Id);
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task UpsertPosts_RemovesProfileOrphanedByReplacement()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();
            await sut.UpsertPosts([CreatePost("p1", "old-profile", "one", "user-1", "posts")]);
            await sut.Save();

            await sut.UpsertPosts([CreatePost("p1", "new-profile", "two", "user-1", "posts")]);

            PostStoreCounts counts = await sut.GetStoreCounts();
            Assert.Equal(1, counts.Posts);
            Assert.Equal(1, counts.Profiles);
            Assert.Equal("new-profile", (await sut.GetByIds(["p1"])).Single().Profile.Id);
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

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
                ]
            );
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
                ]
            );
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

    [Fact]
    public async Task Setup_UsesWalMode_OnLocalDisk()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();

            string dbPath = Path.Combine(root, "data", "post", "posts.db");

            await using SqliteConnection connection = new($"Data Source={dbPath}");
            await connection.OpenAsync();

            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";

            string? mode = (await command.ExecuteScalarAsync())?.ToString();
            Assert.Equal("wal", mode);
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Setup_RebuildsLegacyAndMissingHashMetadata_AndRemovesOrphans()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();
            await sut.UpsertPosts(
                [
                    CreatePost("p1", "profile-1", "one", "user-1", "posts"),
                    CreatePost("p2", "profile-1", "two", "user-1", "posts"),
                ]
            );
            await sut.Save();

            string dbPath = Path.Combine(root, "data", "post", "posts.db");

            await using (SqliteConnection connection = new($"Data Source={dbPath}"))
            {
                await connection.OpenAsync();
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    "UPDATE post_meta SET hash = 'legacy', deleted = 1 WHERE id = 'p1';"
                    + "DELETE FROM post_meta WHERE id = 'p2';"
                    + "INSERT INTO post_meta (id, hash, deleted) VALUES ('orphan', 'legacy', 0);";
                await command.ExecuteNonQueryAsync();
            }

            await sut.Setup();

            Dictionary<string, string> hashes = await sut.GetHashesById();
            Assert.Equal(2, hashes.Count);
            Assert.All(hashes.Values, hash => Assert.StartsWith("v2:", hash));
            Assert.True((await sut.GetByIds(["p1"])).Single().Deleted);
            Assert.False((await sut.GetByIds(["p2"])).Single().Deleted);
        }
        finally
        {
            await sut.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task History_RoundTrips_AndChangesHash()
    {
        (SqlitePostData sut, string root) = CreateSut();

        try
        {
            await sut.Setup();
            await sut.AddPosts(
                "user-1",
                "posts",
                [CreatePost("p1", "profile-1", "old", "user-1", "posts")]
            );
            await sut.Save();
            string oldHash = (await sut.GetHashesById())["p1"];

            await sut.AddPosts(
                "user-1",
                "posts",
                [CreatePost("p1", "profile-1", "new", "user-1", "posts")]
            );
            await sut.Save();

            Post stored = (await sut.GetByIds(["p1"])).Single();
            Change history = Assert.Single(stored.Changes);
            Assert.Equal("old", history.Data!.Description);
            Assert.NotEqual(oldHash, (await sut.GetHashesById())["p1"]);
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

        PostHashingService postHashingService = new(new PostChangeComputationService());
        PostMergeExecutionService postMergeExecutionService = new(
            new PostMergeResolutionService(new PostMergeService()),
            new PostMergeApplyPlanningService()
        );

        SqlitePostData sut = new(
            NullLogger<SqlitePostData>.Instance,
            storage,
            partition,
            new PostStoreMergeMutationService(postMergeExecutionService, postHashingService),
            new PostSoftDeleteExecutionService(
                new PostIdentifierFilterService(),
                new PostSoftDeleteSelectionService()
            ),
            new PostSnapshotNormalizationService(),
            postHashingService,
            new PostChangeComputationService(),
            new PostChangeReadModelProjectionService(),
            new PostIdentifierFilterService(),
            new TestDateTimeProvider()
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

    private sealed class TestDateTimeProvider : Backup.Application.Core.IDateTimeProvider
    {
        public DateTime Now => DateTime.UtcNow;
    }
}
