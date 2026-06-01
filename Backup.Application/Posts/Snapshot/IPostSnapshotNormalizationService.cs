using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostSnapshotNormalizationService
{
    IReadOnlyList<Post> Normalize(IReadOnlyCollection<Post> posts);
}
