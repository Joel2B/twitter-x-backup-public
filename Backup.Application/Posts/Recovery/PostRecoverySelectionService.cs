using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public class PostRecoverySelectionService : IPostRecoverySelectionService
{
    public PostRecoverySelection Select(
        bool recoveryEnabled,
        IReadOnlyCollection<PostRecoveryLog> logs,
        int maxPosts
    )
    {
        if (!recoveryEnabled)
            return PostRecoverySelection.Disabled;

        if (logs.Count == 0 || maxPosts <= 0)
            return new PostRecoverySelection();

        List<string> ids = logs.Where(log =>
                log.Messages.Any(message =>
                    string.Equals(message, "NotFound", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(message, "Forbidden", StringComparison.OrdinalIgnoreCase)
                )
            )
            .Select(log => log.PostId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Take(maxPosts)
            .ToList();

        return new PostRecoverySelection { PostIds = ids };
    }
}
