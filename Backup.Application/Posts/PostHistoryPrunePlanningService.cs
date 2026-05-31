using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostHistoryPrunePlanningService(
    IPostHistoryPrunePolicyService postHistoryPrunePolicyService
) : IPostHistoryPrunePlanningService
{
    private readonly IPostHistoryPrunePolicyService _postHistoryPrunePolicyService =
        postHistoryPrunePolicyService;

    public PostHistoryPrunePlan Plan(
        IReadOnlyList<PostHistoryPath> paths,
        int keepDays,
        int keepCount
    )
    {
        int normalizedKeepDays = Math.Max(1, keepDays);
        int normalizedKeepCount = Math.Max(0, keepCount);
        int distinctDayCount = paths.Select(path => path.Date.Date).Distinct().Count();
        IReadOnlyList<string> pathsToRemove = _postHistoryPrunePolicyService.GetPathsToRemove(
            paths,
            normalizedKeepDays,
            normalizedKeepCount
        );

        return new PostHistoryPrunePlan
        {
            NormalizedKeepDays = normalizedKeepDays,
            NormalizedKeepCount = normalizedKeepCount,
            DistinctDayCount = distinctDayCount,
            PathsToRemove = pathsToRemove,
        };
    }
}
