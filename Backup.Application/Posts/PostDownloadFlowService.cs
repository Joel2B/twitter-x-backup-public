using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public class PostDownloadFlowService : IPostDownloadFlowService
{
    public PostDownloadPlan CreatePlan(
        int defaultQueryCount,
        int defaultTotalCount,
        string? defaultCursor,
        PostDownloadResumePoint? resumePoint
    )
    {
        int queryCount = resumePoint?.QueryCount ?? defaultQueryCount;
        int totalCount = resumePoint?.TotalCount ?? defaultTotalCount;
        string? cursor = resumePoint?.Cursor ?? defaultCursor;

        return new PostDownloadPlan
        {
            QueryCount = queryCount,
            TotalCount = totalCount,
            DownloadedCount = 0,
            Cursor = cursor,
        };
    }

    public bool ShouldContinue(PostDownloadPlan plan) => plan.DownloadedCount < plan.TotalCount;

    public PostDownloadPageDecision DecidePage(
        bool hasValidPage,
        int attemptNumber,
        int maxAttempts,
        bool hasResumePoint
    )
    {
        if (hasValidPage)
            return new PostDownloadPageDecision { Outcome = PostDownloadPageOutcome.Success };

        if (attemptNumber < maxAttempts)
            return new PostDownloadPageDecision { Outcome = PostDownloadPageOutcome.Retry };

        return new PostDownloadPageDecision
        {
            Outcome = PostDownloadPageOutcome.Abort,
            ShouldFlushDump = hasResumePoint,
        };
    }

    public void ApplySuccess(PostDownloadPlan plan, string nextCursor)
    {
        plan.DownloadedCount += plan.QueryCount;
        plan.Cursor = nextCursor;
    }
}
