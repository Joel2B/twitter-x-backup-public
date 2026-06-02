using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;
using Backup.Domain.Posts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.Tests;

public class BulkApplicationServicesTests
{
    [Fact]
    public async Task Phase1Service_SavesPostsAndMarksPhaseAsCompleted_WhenCursorEnds()
    {
        BulkPhase1Service sut = new(NullLogger<BulkPhase1Service>.Instance);
        FakeBulkPhase1Command command = new();
        command.Bulks.Add(
            new BulkItem
            {
                UserName = "user1",
                UserId = "u1",
                UserStatus = BulkUserStatus.Active,
                Phase1Order = 0,
                Cursor = "CURSOR_1",
            }
        );
        command.Verify.Enqueue(true);
        command.Results.Enqueue(new ParseResult([CreatePost("p1", mediaCount: 2)], null));

        await sut.Run(
            command,
            new BulkPhase1Options
            {
                UsersPerCycle = 0,
                SavePerAction = 100,
                ApiPerCycle = 1,
                MediaPerApi = 20,
                MaxCountPost = 100,
                ApiRetryCount = 1,
            },
            origin: "origin-1",
            CancellationToken.None
        );

        Assert.Single(command.AddCalls);
        Assert.Equal("u1", command.AddCalls[0].UserId);
        Assert.Equal("origin-1", command.AddCalls[0].Origin);
        Assert.Equal(1, command.SavePostsCalls);
        Assert.Equal(1, command.SaveBulksCalls);
        Assert.Null(command.Bulks[0].Phase1Order);
        Assert.Null(command.Bulks[0].Cursor);
    }

    [Fact]
    public async Task Phase2Service_ProcessesDeltaAndUpdatesTotal()
    {
        BulkPhase2Service sut = new();
        FakeBulkPhase2Command command = new();
        command.Bulks.Add(
            new BulkItem
            {
                UserName = "user1",
                UserId = "u1",
                UserStatus = BulkUserStatus.Active,
                Phase2Order = 0,
                Total = 1,
                Cursor = "CURSOR_1",
            }
        );

        command.Verify.Enqueue(true); // initial
        command.Verify.Enqueue(true); // loop #1
        command.Verify.Enqueue(true); // loop #2

        command.Results.Enqueue(new ParseResult([CreatePost("p1", mediaCount: 3)], "NEXT_1"));
        command.Results.Enqueue(new ParseResult([CreatePost("p2", mediaCount: 3)], null));

        await sut.Run(
            command,
            new BulkPhase2Options
            {
                UsersPerPhase2 = 10,
                SavePerAction = 100,
                MediaPerApi = 20,
                MaxCountPostPhase2 = 100,
                ApiRetryCount = 1,
            },
            origin: "origin-2",
            CancellationToken.None
        );

        Assert.Equal(2, command.AddCalls.Count);
        Assert.Equal(1, command.SavePostsCalls);
        Assert.Equal(1, command.SaveBulksCalls);
        Assert.Null(command.Bulks[0].Phase2Order);
        Assert.Equal(3, command.Bulks[0].Total);
    }

    [Fact]
    public async Task ImportService_ImportsOnlyMediaAndNonExistingUsers()
    {
        BulkImportService sut = new();
        FakeBulkImportCommand command = new();

        command.Sources.Add(new BulkSourceItem { UserName = "keep", Type = BulkSourceType.Media });
        command.Sources.Add(
            new BulkSourceItem { UserName = "skip-status", Type = BulkSourceType.Status }
        );
        command.Sources.Add(
            new BulkSourceItem { UserName = "existing", Type = BulkSourceType.Media }
        );

        command.Bulks.Add(
            new BulkItem { UserName = "existing", UserStatus = BulkUserStatus.Active }
        );
        command.Verify.Enqueue(true);
        command.UsersByName["keep"] = new ParseUser(new PostUser { Id = "u-keep", MediaCount = 7 });

        await sut.Run(command, new BulkImportOptions { UsersPerCycle = 0 }, CancellationToken.None);

        Assert.Equal(2, command.Bulks.Count);
        BulkItem added = Assert.Single(command.Bulks, item => item.UserName == "keep");
        Assert.Equal("u-keep", added.UserId);
        Assert.Equal(BulkUserStatus.Active, added.UserStatus);
        Assert.Equal(7, added.Total);
        Assert.Equal(1, command.SaveBulksCalls);
    }

    [Fact]
    public async Task VerifyService_BuildsRowsFromCounts()
    {
        BulkVerifyService sut = new();
        FakeBulkVerifyCommand command = new();
        command.Bulks.Add(
            new BulkItem
            {
                UserName = "user1",
                UserId = "u1",
                UserStatus = BulkUserStatus.Active,
                Phase1Order = null,
                Total = 10,
            }
        );
        command.PostCounts["u1"] = 8;

        IReadOnlyList<BulkVerifyRow> rows = await sut.Run(command);

        BulkVerifyRow row = Assert.Single(rows);
        Assert.Equal("u1", row.UserId);
        Assert.Equal(10, row.TotalBulk);
        Assert.Equal(8, row.TotalPost);
    }

    [Fact]
    public async Task Phase2ResetService_SetsPhase2ToZero_WhenAllActiveAreCompleted()
    {
        BulkPhase2ResetService sut = new();
        FakeBulkPhase2ResetCommand command = new();
        command.Bulks.Add(
            new BulkItem
            {
                UserName = "user1",
                UserStatus = BulkUserStatus.Active,
                Phase2Order = null,
            }
        );

        await sut.Run(command);

        Assert.Equal(0, command.Bulks[0].Phase2Order);
        Assert.Equal(1, command.SaveBulksCalls);
    }

    private static Post CreatePost(string id, int mediaCount) =>
        new()
        {
            Id = id,
            Profile = new PostProfile
            {
                Id = "profile-1",
                UserName = "user1",
                Count = new PostCount { Media = mediaCount },
            },
            Description = "desc",
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "2026-01-01T00:00:00Z",
        };

    private sealed class FakeBulkPhase1Command : IBulkPhase1Command
    {
        public List<BulkItem> Bulks { get; } = [];
        public Queue<bool> Verify { get; } = [];
        public Queue<ParseResult?> Results { get; } = [];
        public List<(string UserId, string Origin, List<Post> Posts)> AddCalls { get; } = [];
        public int SavePostsCalls { get; private set; }
        public int SaveBulksCalls { get; private set; }

        public Task<int> GetPostCount() => Task.FromResult(0);

        public Task<IReadOnlyList<BulkItem>> GetBulks() =>
            Task.FromResult<IReadOnlyList<BulkItem>>(Bulks);

        public Task SaveBulks(IReadOnlyList<BulkItem> bulks)
        {
            SaveBulksCalls++;
            return Task.CompletedTask;
        }

        public Task<bool> VerifyApi() => Task.FromResult(Verify.Count == 0 || Verify.Dequeue());

        public Task<ParseResult?> GetUserMedia(
            string userId,
            string origin,
            int count,
            string? cursor,
            CancellationToken cancellationToken
        ) => Task.FromResult(Results.Count == 0 ? null : Results.Dequeue());

        public Task AddPosts(string userId, string origin, List<Post> posts)
        {
            AddCalls.Add((userId, origin, posts));
            return Task.CompletedTask;
        }

        public Task SavePosts()
        {
            SavePostsCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBulkPhase2Command : IBulkPhase2Command
    {
        public List<BulkItem> Bulks { get; } = [];
        public Queue<bool> Verify { get; } = [];
        public Queue<ParseResult?> Results { get; } = [];
        public List<(string UserId, string Origin, List<Post> Posts)> AddCalls { get; } = [];
        public int SavePostsCalls { get; private set; }
        public int SaveBulksCalls { get; private set; }

        public Task<IReadOnlyList<BulkItem>> GetBulks() =>
            Task.FromResult<IReadOnlyList<BulkItem>>(Bulks);

        public Task SaveBulks(IReadOnlyList<BulkItem> bulks)
        {
            SaveBulksCalls++;
            return Task.CompletedTask;
        }

        public Task<bool> VerifyApi() => Task.FromResult(Verify.Count == 0 || Verify.Dequeue());

        public Task<ParseResult?> GetUserMedia(
            string userId,
            string origin,
            int count,
            string? cursor,
            CancellationToken cancellationToken
        ) => Task.FromResult(Results.Count == 0 ? null : Results.Dequeue());

        public Task AddPosts(string userId, string origin, List<Post> posts)
        {
            AddCalls.Add((userId, origin, posts));
            return Task.CompletedTask;
        }

        public Task SavePosts()
        {
            SavePostsCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBulkImportCommand : IBulkImportCommand
    {
        public List<BulkSourceItem> Sources { get; } = [];
        public List<BulkItem> Bulks { get; } = [];
        public Queue<bool> Verify { get; } = [];
        public Dictionary<string, ParseUser?> UsersByName { get; } = [];
        public int SaveBulksCalls { get; private set; }

        public Task<IReadOnlyList<BulkSourceItem>> GetSources() =>
            Task.FromResult<IReadOnlyList<BulkSourceItem>>(Sources);

        public Task<IReadOnlyList<BulkItem>> GetBulks() =>
            Task.FromResult<IReadOnlyList<BulkItem>>(Bulks);

        public Task SaveBulks(IReadOnlyList<BulkItem> bulks)
        {
            SaveBulksCalls++;
            List<BulkItem> snapshot = bulks.ToList();
            Bulks.Clear();
            Bulks.AddRange(snapshot);
            return Task.CompletedTask;
        }

        public Task<bool> VerifyApi() => Task.FromResult(Verify.Count == 0 || Verify.Dequeue());

        public Task<ParseUser?> GetUserByUser(
            string userName,
            CancellationToken cancellationToken
        ) => Task.FromResult(UsersByName.TryGetValue(userName, out ParseUser? user) ? user : null);
    }

    private sealed class FakeBulkVerifyCommand : IBulkVerifyCommand
    {
        public List<BulkItem> Bulks { get; } = [];
        public Dictionary<string, int> PostCounts { get; } = [];

        public Task<IReadOnlyList<BulkItem>> GetBulks(
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<BulkItem>>(Bulks);

        public Task<Dictionary<string, int>> GetPostCountsByProfileIds(
            IReadOnlyCollection<string> profileIds,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                PostCounts
                    .Where(kvp => profileIds.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            );
    }

    private sealed class FakeBulkPhase2ResetCommand : IBulkPhase2ResetCommand
    {
        public List<BulkItem> Bulks { get; } = [];
        public int SaveBulksCalls { get; private set; }

        public Task<IReadOnlyList<BulkItem>> GetBulks(
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<BulkItem>>(Bulks);

        public Task SaveBulks(
            IReadOnlyList<BulkItem> bulks,
            CancellationToken cancellationToken = default
        )
        {
            SaveBulksCalls++;
            List<BulkItem> snapshot = bulks.ToList();
            Bulks.Clear();
            Bulks.AddRange(snapshot);
            return Task.CompletedTask;
        }
    }
}
