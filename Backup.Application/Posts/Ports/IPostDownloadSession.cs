using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts.Ports;

public interface IPostDownloadSession
{
    int DefaultQueryCount { get; }
    int DefaultTotalCount { get; }
    string? DefaultCursor { get; }
    Task<PostDownloadResumePoint?> GetResumePoint(CancellationToken cancellationToken);
    void ApplyPlan(PostDownloadPlan plan);
    void SetCursor(string cursor);
    void OnPageCycle(PostDownloadPlan plan);
    void OnAttempt(int attemptNumber);
    Task<PostDownloadPageResult> FetchPage(CancellationToken cancellationToken);
    Task PersistResumeState(PostDownloadPageResult pageResult, CancellationToken cancellationToken);
    Task FlushResumeState(CancellationToken cancellationToken);
    Task AddPosts(IReadOnlyCollection<Post> posts);
}
