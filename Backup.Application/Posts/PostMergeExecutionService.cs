using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostMergeExecutionService(
    IPostMergeResolutionService postMergeResolutionService,
    IPostMergeApplyPlanningService postMergeApplyPlanningService
) : IPostMergeExecutionService
{
    private readonly IPostMergeResolutionService _postMergeResolutionService =
        postMergeResolutionService;
    private readonly IPostMergeApplyPlanningService _postMergeApplyPlanningService =
        postMergeApplyPlanningService;

    public PostMergeApplyPlan BuildApplyPlan(
        string userId,
        string origin,
        IReadOnlyList<Post> incoming,
        IReadOnlyDictionary<string, Post> existing,
        MergeOptions options
    )
    {
        IReadOnlyList<PostMergeResolutionItem> mergeResult = _postMergeResolutionService.Resolve(
            userId,
            origin,
            incoming,
            existing,
            options
        );

        return _postMergeApplyPlanningService.BuildPlan(
            mergeResult,
            existing.Keys.ToHashSet(StringComparer.Ordinal)
        );
    }
}
