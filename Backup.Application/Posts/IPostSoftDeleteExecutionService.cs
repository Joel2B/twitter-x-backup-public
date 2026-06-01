using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostSoftDeleteExecutionService
{
    IReadOnlySet<string> SelectIdsToMarkDeleted(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds,
        IReadOnlyList<Post> posts,
        IReadOnlyDictionary<string, bool> deletedById
    );
}
