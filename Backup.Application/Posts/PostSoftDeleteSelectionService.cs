using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostSoftDeleteSelectionService : IPostSoftDeleteSelectionService
{
    public IReadOnlyCollection<string> SelectIds(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds,
        IReadOnlyCollection<Post> posts
    )
    {
        if (posts.Count == 0)
            return [];

        HashSet<string> keep = keepPostIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        return posts
            .Where(post =>
                post.Index.TryGetValue(userId, out Dictionary<string, IndexData>? index)
                && index.ContainsKey(origin)
                && !post.Deleted
                && !keep.Contains(post.Id)
            )
            .Select(post => post.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
