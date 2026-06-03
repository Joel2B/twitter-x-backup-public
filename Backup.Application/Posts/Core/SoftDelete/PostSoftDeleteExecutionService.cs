using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostSoftDeleteExecutionService(
    IPostIdentifierFilterService postIdentifierFilterService,
    IPostSoftDeleteSelectionService postSoftDeleteSelectionService
) : IPostSoftDeleteExecutionService
{
    private readonly IPostIdentifierFilterService _postIdentifierFilterService =
        postIdentifierFilterService;
    private readonly IPostSoftDeleteSelectionService _postSoftDeleteSelectionService =
        postSoftDeleteSelectionService;

    public IReadOnlySet<string> SelectIdsToMarkDeleted(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds,
        IReadOnlyList<Post> posts,
        IReadOnlyDictionary<string, bool> deletedById
    )
    {
        IReadOnlySet<string> keep = _postIdentifierFilterService.Normalize(keepPostIds);
        HashSet<string> selected = _postSoftDeleteSelectionService
            .SelectIds(userId, origin, keep, posts)
            .ToHashSet(StringComparer.Ordinal);

        selected.RemoveWhere(id => deletedById.TryGetValue(id, out bool isDeleted) && isDeleted);

        return selected;
    }
}
