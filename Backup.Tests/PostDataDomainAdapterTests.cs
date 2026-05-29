using Backup.Domain.Posts;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Posts.Adapters;
using AppPosts = Backup.Infrastructure.Models.Posts;

namespace Backup.Tests;

public class PostDataDomainAdapterTests
{
    [Fact]
    public async Task AddPosts_MapsDomainPostsToInfrastructurePosts()
    {
        FakePostData fake = new();
        PostDataDomainAdapter adapter = new(fake);

        List<Post> posts =
        [
            new Post
            {
                Id = "p1",
                Profile = new PostProfile { Id = "u1", UserName = "user1" },
                Description = "desc",
                Retweeted = false,
                Favorited = false,
                Bookmarked = false,
                CreatedAt = "Sun May 24 04:00:00 +0000 2026",
                Deleted = false,
            },
        ];

        await adapter.AddPosts("user-1", "origin-1", posts, new MergeOptions { Index = false });

        Assert.Single(fake.AddCalls);
        var call = fake.AddCalls[0];
        Assert.Equal("user-1", call.UserId);
        Assert.Equal("origin-1", call.Origin);
        Assert.Single(call.Posts);
        Assert.Equal("p1", call.Posts[0].Id);
        Assert.Equal("u1", call.Posts[0].Profile.Id);
        Assert.NotNull(call.Options);
        Assert.False(call.Options!.Index);
    }

    [Fact]
    public async Task GetAll_MapsInfrastructurePostsToDomainPosts()
    {
        FakePostData fake = new()
        {
            NextAll = new List<AppPosts.Post>
            {
                new AppPosts.Post
                {
                    Id = "x1",
                    Profile = new AppPosts.PostProfile { Id = "u9", UserName = "name9" },
                    Description = "hello",
                    Retweeted = true,
                    Favorited = false,
                    Bookmarked = true,
                    CreatedAt = "Sun May 24 04:00:00 +0000 2026",
                    Deleted = false,
                },
            },
        };
        PostDataDomainAdapter adapter = new(fake);

        List<Post>? posts = await adapter.GetAll();

        Post post = Assert.Single(posts!);
        Assert.Equal("x1", post.Id);
        Assert.Equal("u9", post.Profile.Id);
        Assert.Equal("hello", post.Description);
    }

    private sealed class FakePostData : IPostData
    {
        public string? Id { get; set; }
        public List<AppPosts.Post>? NextAll { get; set; } = [];

        public List<(
            string UserId,
            string Origin,
            List<AppPosts.Post> Posts,
            AppPosts.MergeOptions? Options
        )> AddCalls { get; } = [];

        public Task<int> GetCount() => Task.FromResult(0);

        public Task<List<AppPosts.Post>?> GetAll() => Task.FromResult(NextAll);

        public Task<List<AppPosts.MediaInput>?> GetMediaInputs() =>
            Task.FromResult<List<AppPosts.MediaInput>?>([]);

        public Task<Dictionary<string, string>> GetHashesById() =>
            Task.FromResult(new Dictionary<string, string>());

        public Task<List<AppPosts.Post>> GetByIds(IReadOnlyCollection<string> ids) =>
            Task.FromResult(new List<AppPosts.Post>());

        public Task<Dictionary<string, int>> GetPostCountsByProfileIds(
            IReadOnlyCollection<string> profileIds
        ) => Task.FromResult(new Dictionary<string, int>());

        public Task AddPosts(
            string userId,
            string origin,
            List<AppPosts.Post> incoming,
            AppPosts.MergeOptions? options = null
        )
        {
            AddCalls.Add((userId, origin, incoming, options));
            return Task.CompletedTask;
        }

        public Task<int> MarkDeletedExcept(
            string userId,
            string origin,
            IReadOnlyCollection<string> keepPostIds
        ) => Task.FromResult(0);

        public Task Reset(List<AppPosts.Post> posts) => Task.CompletedTask;

        public Task UpsertPosts(List<AppPosts.Post> posts) => Task.CompletedTask;

        public Task Save() => Task.CompletedTask;
        public Task Prune() => Task.CompletedTask;
    }
}
