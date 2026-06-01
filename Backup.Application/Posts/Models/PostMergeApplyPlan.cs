namespace Backup.Application.Posts.Models;

public sealed class PostMergeApplyPlan
{
    public IReadOnlyList<PostMergeApplyPlanItem> Items { get; init; } = [];
}
