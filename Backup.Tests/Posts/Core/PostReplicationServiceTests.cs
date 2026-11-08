using Backup.Application.Posts;
using Backup.Application.Posts.Ports;
using Backup.Domain.Posts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.Tests;

public sealed class PostReplicationServiceTests
{
    [Fact]
    public async Task Replicate_DeletesExtraIdsWithoutFullReset()
    {
        FakeStore source = new("source", true, [Post("1"), Post("2")]);
        FakeStore target = new("target", false, [Post("1"), Post("extra")]);
        PostReplicationService sut = CreateSut();

        await sut.Replicate([source, target]);

        Assert.Equal(["1", "2"], target.Posts.Keys.Order().ToArray());
        Assert.Equal(["extra"], target.DeletedIds);
        Assert.Equal(2, target.SaveCalls);
        Assert.Equal(0, source.GetAllCalls);
        Assert.Equal(0, target.ResetCalls);
    }

    [Fact]
    public async Task Replicate_DoesNotDeleteExtras_WhenSourceReturnsPartialChunk()
    {
        FakeStore source = new("source", true, [Post("1"), Post("2")]) { OmitRequestedId = "2" };
        FakeStore target = new("target", false, [Post("1"), Post("extra")]);
        PostReplicationService sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Replicate([source, target]));

        Assert.Contains("extra", target.Posts.Keys);
        Assert.Empty(target.DeletedIds);
    }

    [Fact]
    public async Task Replicate_RewritesSource_WhenStoredHashIsStale()
    {
        FakeStore source = new("source", true, [Post("1")]);
        FakeStore target = new("target", false, [Post("1")]);
        source.HashOverrides["1"] = "stale";
        target.HashOverrides["1"] = "different";

        await CreateSut().Replicate([source, target]);

        Assert.Equal(1, source.UpsertCalls);
        Assert.Equal(1, source.SaveCalls);
        Assert.Equal(1, target.UpsertCalls);
    }

    private static PostReplicationService CreateSut()
    {
        PostChangeComputationService changes = new();
        return new PostReplicationService(
            NullLogger<PostReplicationService>.Instance,
            new PostHashingService(changes)
        );
    }

    private static Post Post(string id) =>
        new()
        {
            Id = id,
            Profile = new()
            {
                Id = "profile",
                UserName = "user",
                Name = "User",
            },
            Description = id,
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "2026-07-22T00:00:00Z",
        };

    private sealed class FakeStore(string id, bool isDefault, IEnumerable<Post> posts)
        : IPostReplicationStore
    {
        public string? Id { get; } = id;
        public bool IsDefault { get; } = isDefault;
        public Dictionary<string, Post> Posts { get; } = posts.ToDictionary(post => post.Id);
        public List<string> DeletedIds { get; } = [];
        public int GetAllCalls { get; private set; }
        public int ResetCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public int UpsertCalls { get; private set; }
        public string? OmitRequestedId { get; init; }
        public Dictionary<string, string> HashOverrides { get; } = [];

        public Task<Dictionary<string, string>> GetHashesById() =>
            Task.FromResult(
                Posts.ToDictionary(
                    entry => entry.Key,
                    entry =>
                        HashOverrides.TryGetValue(entry.Key, out string? hash)
                            ? hash
                            : new PostHashingService(new PostChangeComputationService()).Compute(
                                entry.Value
                            )
                )
            );

        public Task<List<Post>?> GetAll()
        {
            GetAllCalls++;
            return Task.FromResult<List<Post>?>([.. Posts.Values]);
        }

        public Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids) =>
            Task.FromResult(
                ids.Where(id => id != OmitRequestedId && Posts.ContainsKey(id))
                    .Select(id => Posts[id])
                    .ToList()
            );

        public Task DeletePosts(IReadOnlyCollection<string> ids)
        {
            foreach (string postId in ids)
            {
                if (Posts.Remove(postId))
                    DeletedIds.Add(postId);
            }

            return Task.CompletedTask;
        }

        public Task UpsertPosts(List<Post> postsToUpsert)
        {
            UpsertCalls++;

            foreach (Post post in postsToUpsert)
            {
                Posts[post.Id] = post;
                HashOverrides.Remove(post.Id);
            }

            return Task.CompletedTask;
        }

        public Task Reset(List<Post> postsToReset)
        {
            ResetCalls++;
            Posts.Clear();

            foreach (Post post in postsToReset)
                Posts[post.Id] = post;

            return Task.CompletedTask;
        }

        public Task Save()
        {
            SaveCalls++;
            return Task.CompletedTask;
        }

        public Task Prune() => Task.CompletedTask;
    }
}
