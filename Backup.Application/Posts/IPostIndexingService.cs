using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostIndexingService
{
    void ApplySequenceIndex(IReadOnlyList<Post> posts, string userId, string origin);
}
