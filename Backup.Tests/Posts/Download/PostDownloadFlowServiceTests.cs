using Backup.Application.Posts;
using Backup.Application.Posts.Models;

namespace Backup.Tests;

public class PostDownloadFlowServiceTests
{
    private readonly IPostDownloadFlowService _service = new PostDownloadFlowService();

    [Fact]
    public void CreatePlan_UsesResumePoint_WhenProvided()
    {
        PostDownloadPlan plan = _service.CreatePlan(
            defaultQueryCount: 20,
            defaultTotalCount: 100,
            defaultCursor: "a",
            resumePoint: new PostDownloadResumePoint
            {
                QueryCount = 50,
                TotalCount = 500,
                Cursor = "resume",
            }
        );

        Assert.Equal(50, plan.QueryCount);
        Assert.Equal(500, plan.TotalCount);
        Assert.Equal("resume", plan.Cursor);
        Assert.Equal(0, plan.DownloadedCount);
    }

    [Fact]
    public void DecidePage_RetriesThenAborts_AfterMaxAttempts()
    {
        PostDownloadPageDecision retryDecision = _service.DecidePage(
            hasValidPage: false,
            attemptNumber: 1,
            maxAttempts: 3,
            hasResumePoint: true
        );
        Assert.Equal(PostDownloadPageOutcome.Retry, retryDecision.Outcome);

        PostDownloadPageDecision abortDecision = _service.DecidePage(
            hasValidPage: false,
            attemptNumber: 3,
            maxAttempts: 3,
            hasResumePoint: true
        );
        Assert.Equal(PostDownloadPageOutcome.Abort, abortDecision.Outcome);
        Assert.True(abortDecision.ShouldFlushDump);
    }

    [Fact]
    public void ApplySuccess_AdvancesCountAndCursor()
    {
        PostDownloadPlan plan = _service.CreatePlan(
            defaultQueryCount: 20,
            defaultTotalCount: 100,
            defaultCursor: null,
            resumePoint: null
        );

        _service.ApplySuccess(plan, "next-1");
        _service.ApplySuccess(plan, "next-2");

        Assert.Equal(40, plan.DownloadedCount);
        Assert.Equal("next-2", plan.Cursor);
        Assert.True(_service.ShouldContinue(plan));
    }
}
