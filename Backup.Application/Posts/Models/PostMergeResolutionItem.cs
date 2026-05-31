using Backup.Domain.Posts;

namespace Backup.Application.Posts.Models;

public sealed class PostMergeResolutionItem
{
    public required string Id { get; init; }

    public required Post MergedPost { get; init; }

    public required bool IsNew { get; init; }

    public required bool HasDataChange { get; init; }

    public required bool HasIndexChange { get; init; }

    public bool HasChanges => HasDataChange || HasIndexChange || IsNew;
}
