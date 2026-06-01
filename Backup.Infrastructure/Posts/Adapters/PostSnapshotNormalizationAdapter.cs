using Backup.Application.Posts;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Posts.Adapters;

internal static class PostSnapshotNormalizationAdapter
{
    public static IReadOnlyList<Post> Normalize(
        IPostSnapshotNormalizationService postSnapshotNormalizationService,
        IReadOnlyCollection<Post> posts
    )
    {
        List<Backup.Domain.Posts.Post> domainPosts = posts.Select(PostReplicationMapper.ToDomain).ToList();
        IReadOnlyList<Backup.Domain.Posts.Post> normalized = postSnapshotNormalizationService.Normalize(
            domainPosts
        );
        return normalized.Select(PostReplicationMapper.ToApp).ToList();
    }
}
