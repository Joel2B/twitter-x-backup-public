using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostDownloadFlowService
{
    PostDownloadPlan CreatePlan(
        int defaultQueryCount,
        int defaultTotalCount,
        string? defaultCursor,
        PostDownloadResumePoint? resumePoint
    );

    bool ShouldContinue(PostDownloadPlan plan);

    PostDownloadPageDecision DecidePage(
        bool hasValidPage,
        int attemptNumber,
        int maxAttempts,
        bool hasResumePoint
    );

    void ApplySuccess(PostDownloadPlan plan, string nextCursor);
}
