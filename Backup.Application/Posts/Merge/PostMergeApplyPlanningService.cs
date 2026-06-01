using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostMergeApplyPlanningService : IPostMergeApplyPlanningService
{
    public PostMergeApplyPlan BuildPlan(
        IReadOnlyList<PostMergeResolutionItem> mergeResult,
        IReadOnlySet<string> existingIds
    )
    {
        List<PostMergeApplyPlanItem> items = [];

        foreach (PostMergeResolutionItem item in mergeResult)
        {
            bool exists = existingIds.Contains(item.Id);
            bool shouldPersist = item.IsNew || (item.HasChanges && exists);

            items.Add(
                new PostMergeApplyPlanItem
                {
                    Id = item.Id,
                    MergedPost = item.MergedPost.Clone(),
                    IsNew = item.IsNew,
                    ShouldPersist = shouldPersist,
                    ShouldLogDataChange = shouldPersist && !item.IsNew && item.HasDataChange,
                    ShouldLogIndexChange = shouldPersist && !item.IsNew && item.HasIndexChange,
                }
            );
        }

        return new PostMergeApplyPlan { Items = items };
    }
}
