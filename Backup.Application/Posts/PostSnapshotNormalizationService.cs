using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostSnapshotNormalizationService : IPostSnapshotNormalizationService
{
    public IReadOnlyList<Post> Normalize(IReadOnlyCollection<Post> posts)
    {
        if (posts.Count == 0)
            return [];

        return posts
            .Where(post => !string.IsNullOrWhiteSpace(post.Id))
            .GroupBy(post => post.Id, StringComparer.Ordinal)
            .Select(group => group.Last().Clone())
            .ToList();
    }
}
