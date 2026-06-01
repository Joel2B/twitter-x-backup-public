using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;

namespace Backup.Tests;

public class PostDownloadCommandServiceTests
{
    [Fact]
    public async Task Execute_RunsHappyPathInOrder()
    {
        List<string> calls = [];
        FakeDownloadOrchestrationService orchestration = new(calls);
        PostDownloadCommandService service = new(orchestration);
        FakePostDownloadCommand command = new(calls);

        await service.Execute(command, CancellationToken.None);

        Assert.Equal(["count", "loaded", "create-session", "orchestrate", "prune", "save"], calls);
        Assert.Same(command.Session, orchestration.LastSession);
    }

    [Fact]
    public async Task Execute_OnOrchestrationError_StillPrunesAndSaves()
    {
        List<string> calls = [];
        FakeDownloadOrchestrationService orchestration = new(calls) { ThrowOnRun = true };
        PostDownloadCommandService service = new(orchestration);
        FakePostDownloadCommand command = new(calls);

        await service.Execute(command, CancellationToken.None);

        Assert.Equal(
            ["count", "loaded", "create-session", "orchestrate", "error", "prune", "save"],
            calls
        );
    }

    private sealed class FakeDownloadOrchestrationService(List<string> calls)
        : IPostDownloadOrchestrationService
    {
        public bool ThrowOnRun { get; set; }
        public IPostDownloadSession? LastSession { get; private set; }

        public Task Run(IPostDownloadSession session, CancellationToken cancellationToken)
        {
            calls.Add("orchestrate");
            LastSession = session;

            if (ThrowOnRun)
                throw new InvalidOperationException("boom");

            return Task.CompletedTask;
        }
    }

    private sealed class FakePostDownloadCommand(List<string> calls) : IPostDownloadCommand
    {
        public FakeDownloadSession Session { get; } = new();

        public Task<int> GetLoadedCount()
        {
            calls.Add("count");
            return Task.FromResult(7);
        }

        public IPostDownloadSession CreateSession()
        {
            calls.Add("create-session");
            return Session;
        }

        public void OnLoadedCount(int count)
        {
            Assert.Equal(7, count);
            calls.Add("loaded");
        }

        public void OnError(Exception exception)
        {
            Assert.IsType<InvalidOperationException>(exception);
            calls.Add("error");
        }

        public Task PruneLogs()
        {
            calls.Add("prune");
            return Task.CompletedTask;
        }

        public Task SavePosts()
        {
            calls.Add("save");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDownloadSession : IPostDownloadSession
    {
        public int DefaultQueryCount => 0;

        public int DefaultTotalCount => 0;

        public string? DefaultCursor => null;

        public Task<PostDownloadResumePoint?> GetResumePoint(CancellationToken cancellationToken) =>
            Task.FromResult<PostDownloadResumePoint?>(null);

        public void ApplyPlan(PostDownloadPlan plan) { }

        public void SetCursor(string cursor) { }

        public void OnPageCycle(PostDownloadPlan plan) { }

        public void OnAttempt(int attemptNumber) { }

        public Task<PostDownloadPageResult> FetchPage(CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task PersistResumeState(
            PostDownloadPageResult pageResult,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public Task FlushResumeState(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddPosts(IReadOnlyCollection<Backup.Domain.Posts.Post> posts) =>
            Task.CompletedTask;
    }
}
