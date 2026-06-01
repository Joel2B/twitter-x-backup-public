using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostHistoryPrunePolicyService
{
    IReadOnlyList<string> GetPathsToRemove(
        IReadOnlyList<PostHistoryPath> paths,
        int keepDays,
        int keepCount
    );
}
