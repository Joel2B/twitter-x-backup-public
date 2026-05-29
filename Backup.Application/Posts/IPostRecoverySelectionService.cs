using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostRecoverySelectionService
{
    PostRecoverySelection Select(
        bool recoveryEnabled,
        IReadOnlyCollection<PostRecoveryLog> logs,
        int maxPosts
    );
}
