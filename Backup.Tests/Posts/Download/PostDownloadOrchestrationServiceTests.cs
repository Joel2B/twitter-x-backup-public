using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;

namespace Backup.Tests;

public class PostDownloadOrchestrationServiceTests
{
    [Fact]
    public async Task Run_TerminalEmptyPage_CompletesWithoutRetry()
    {
        FakeSession session = new(isTerminal: true);
        PostDownloadOrchestrationService service = new(new PostDownloadFlowService());

        await service.Run(session, CancellationToken.None);

        Assert.Equal(1, session.Attempts);
        Assert.Equal(1, session.AddCalls);
    }

    [Fact]
    public async Task Run_NonTerminalEmptyPage_RetriesThreeTimes()
    {
        FakeSession session = new(isTerminal: false);
        PostDownloadOrchestrationService service = new(new PostDownloadFlowService());

        await service.Run(session, CancellationToken.None);

        Assert.Equal(3, session.Attempts);
        Assert.Equal(0, session.AddCalls);
    }

    private sealed class FakeSession(bool isTerminal) : IPostDownloadSession
    {
        public int Attempts { get; private set; }
        public int AddCalls { get; private set; }
        public int DefaultQueryCount => 100;
        public int DefaultTotalCount => 500;
        public string? DefaultCursor => null;

        public Task<PostDownloadResumePoint?> GetResumePoint(CancellationToken cancellationToken) =>
            Task.FromResult<PostDownloadResumePoint?>(null);

        public void ApplyPlan(PostDownloadPlan plan) { }
        public void SetCursor(string cursor) { }
        public void OnPageCycle(PostDownloadPlan plan) { }
        public void OnAttempt(int attemptNumber) => Attempts++;

        public Task<PostDownloadPageResult> FetchPage(CancellationToken cancellationToken) =>
            Task.FromResult(
                new PostDownloadPageResult
                {
                    Posts = [],
                    NextCursor = isTerminal ? "bottom" : null,
                    RawResponse = "{}",
                    IsTerminal = isTerminal,
                }
            );

        public Task PersistResumeState(
            PostDownloadPageResult pageResult,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public Task FlushResumeState(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddPosts(IReadOnlyCollection<Backup.Domain.Posts.Post> posts)
        {
            AddCalls++;
            return Task.CompletedTask;
        }
    }
}
