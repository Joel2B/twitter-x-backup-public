using Backup.Application.Posts;
using Backup.Application.Posts.Ports;
using Backup.Domain.Posts;

namespace Backup.Tests;

public class PostRecoveryCommandServiceTests
{
    [Fact]
    public async Task Execute_WhenNoPosts_InvokesNoPostsHook()
    {
        List<string> calls = [];
        FakeRecoveryOrchestrationService orchestration = new(calls) { PostsToReturn = [] };
        PostRecoveryCommandService service = new(orchestration);
        FakePostRecoveryCommand command = new(calls);

        await service.Execute(command, CancellationToken.None);

        Assert.Equal(["create-session", "recover", "none"], calls);
    }

    [Fact]
    public async Task Execute_WhenPosts_MergesThenSaves()
    {
        List<string> calls = [];
        FakeRecoveryOrchestrationService orchestration = new(calls)
        {
            PostsToReturn = [CreatePost("1")],
        };
        PostRecoveryCommandService service = new(orchestration);
        FakePostRecoveryCommand command = new(calls);

        await service.Execute(command, CancellationToken.None);

        Assert.Equal(["create-session", "recover", "merge:1", "merged:1", "save"], calls);
    }

    [Fact]
    public async Task Execute_OnError_InvokesErrorHook()
    {
        List<string> calls = [];
        FakeRecoveryOrchestrationService orchestration = new(calls) { ThrowOnRecover = true };
        PostRecoveryCommandService service = new(orchestration);
        FakePostRecoveryCommand command = new(calls);

        await service.Execute(command, CancellationToken.None);

        Assert.Equal(["create-session", "recover", "error"], calls);
    }

    private static Post CreatePost(string id) =>
        new()
        {
            Id = id,
            Profile = new PostProfile
            {
                Id = "p",
                UserName = "u",
                Name = "n",
                BannerUrl = "b",
                ImageUrl = "i",
                Following = false,
            },
            Description = "d",
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "now",
        };

    private sealed class FakeRecoveryOrchestrationService(List<string> calls)
        : IPostRecoveryOrchestrationService
    {
        public bool ThrowOnRecover { get; set; }
        public IReadOnlyCollection<Post> PostsToReturn { get; set; } = [];

        public Task<IReadOnlyCollection<Post>> Recover(
            IPostRecoverySession session,
            CancellationToken cancellationToken
        )
        {
            calls.Add("recover");
            if (ThrowOnRecover)
                throw new InvalidOperationException("boom");

            return Task.FromResult(PostsToReturn);
        }
    }

    private sealed class FakePostRecoveryCommand(List<string> calls) : IPostRecoveryCommand
    {
        public IPostRecoverySession CreateSession()
        {
            calls.Add("create-session");
            return new FakeRecoverySession();
        }

        public Task MergeRecoveredPosts(IReadOnlyCollection<Post> posts)
        {
            calls.Add($"merge:{posts.Count}");
            return Task.CompletedTask;
        }

        public Task SavePosts()
        {
            calls.Add("save");
            return Task.CompletedTask;
        }

        public void OnNoPostsRecovered() => calls.Add("none");

        public void OnPostsMerged(int count) => calls.Add($"merged:{count}");

        public void OnError(Exception exception)
        {
            Assert.IsType<InvalidOperationException>(exception);
            calls.Add("error");
        }
    }

    private sealed class FakeRecoverySession : IPostRecoverySession
    {
        public bool RecoveryEnabled => true;

        public int MaxRecoveryPosts => 10;

        public bool CanDownloadTweetDetail => true;

        public Task<
            IReadOnlyCollection<Backup.Application.Posts.Models.PostRecoveryLog>
        > GetRecoveryLogs() => throw new NotImplementedException();

        public Task<Post?> DownloadPost(string postId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task MarkRecovered(string postId) => Task.CompletedTask;

        public Task DelayBetweenDownloads(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void OnRecoveryDisabled() { }

        public void OnSelectedPosts(int count) { }

        public void OnTweetDetailUnavailable() { }

        public void OnRecoveredPost(string postId) { }
    }
}
