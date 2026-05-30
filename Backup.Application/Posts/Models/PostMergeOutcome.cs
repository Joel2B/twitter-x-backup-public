using Backup.Domain.Posts;

namespace Backup.Application.Posts.Models;

public sealed class PostMergeOutcome
{
    public required Post MergedPost { get; init; }
    public required bool HasDataChange { get; init; }
    public required bool HasIndexChange { get; init; }
    public bool HasChanges => HasDataChange || HasIndexChange;
    public Change? Change { get; init; }
}
