using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostSoftDeleteSelectionService
{
    IReadOnlyCollection<string> SelectIds(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds,
        IReadOnlyCollection<Post> posts
    );
}
