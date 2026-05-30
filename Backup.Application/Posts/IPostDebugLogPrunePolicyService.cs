using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostDebugLogPrunePolicyService
{
    IReadOnlyList<string> GetPathsToRemove(
        IReadOnlyList<PostHistoryPath> paths,
        int retainedCountLimit
    );
}
