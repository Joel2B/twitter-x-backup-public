using Backup.Domain.Posts;

namespace Backup.Application.Posts.Models;

public sealed class PostMergeApplyPlanItem
{
    public required string Id { get; init; }

    public required Post MergedPost { get; init; }

    public required bool IsNew { get; init; }

    public required bool ShouldPersist { get; init; }

    public required bool ShouldLogDataChange { get; init; }

    public required bool ShouldLogIndexChange { get; init; }
}
