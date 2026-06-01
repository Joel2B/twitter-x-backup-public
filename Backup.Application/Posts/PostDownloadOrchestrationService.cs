using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public class PostDownloadOrchestrationService(IPostDownloadFlowService postDownloadFlowService)
    : IPostDownloadOrchestrationService
{
    private readonly IPostDownloadFlowService _postDownloadFlowService = postDownloadFlowService;

    public async Task Run(IPostDownloadSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        PostDownloadResumePoint? resumePoint = await session.GetResumePoint(cancellationToken);
        bool hasResumePoint = resumePoint is not null;

        PostDownloadPlan plan = _postDownloadFlowService.CreatePlan(
            session.DefaultQueryCount,
            session.DefaultTotalCount,
            session.DefaultCursor,
            resumePoint
        );

        session.ApplyPlan(plan);

        while (_postDownloadFlowService.ShouldContinue(plan))
        {
            session.OnPageCycle(plan);

            const int maxAttempts = 3;
            int attemptCount = 0;
            PostDownloadPageResult pageResult = new()
            {
                Posts = [],
                RawResponse = "",
                NextCursor = null,
            };

            while (true)
            {
                session.OnAttempt(attemptCount + 1);
                pageResult = await session.FetchPage(cancellationToken);

                PostDownloadPageDecision decision = _postDownloadFlowService.DecidePage(
                    pageResult.HasValidPage,
                    attemptCount + 1,
                    maxAttempts,
                    hasResumePoint
                );

                if (decision.Outcome == PostDownloadPageOutcome.Retry)
                {
                    attemptCount++;
                    await Task.Delay(1 * 1000, cancellationToken);
                    continue;
                }

                if (decision.Outcome == PostDownloadPageOutcome.Abort)
                {
                    if (decision.ShouldFlushDump && hasResumePoint)
                        await session.FlushResumeState(cancellationToken);

                    return;
                }

                if (hasResumePoint)
                    await session.PersistResumeState(pageResult, cancellationToken);

                break;
            }

            await session.AddPosts(pageResult.Posts);

            _postDownloadFlowService.ApplySuccess(plan, pageResult.NextCursor!);
            session.SetCursor(plan.Cursor!);

            await Task.Delay(5 * 1000, cancellationToken);
        }
    }
}
