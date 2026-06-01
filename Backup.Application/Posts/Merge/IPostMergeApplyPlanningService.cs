using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostMergeApplyPlanningService
{
    PostMergeApplyPlan BuildPlan(
        IReadOnlyList<PostMergeResolutionItem> mergeResult,
        IReadOnlySet<string> existingIds
    );
}
