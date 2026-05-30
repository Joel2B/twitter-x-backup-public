using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostDebugLogPrunePolicyService : IPostDebugLogPrunePolicyService
{
    public IReadOnlyList<string> GetPathsToRemove(
        IReadOnlyList<PostHistoryPath> paths,
        int retainedCountLimit
    )
    {
        if (paths.Count == 0)
            return [];

        int normalizedRetainedCountLimit = Math.Max(0, retainedCountLimit);

        IReadOnlyList<PostHistoryPath> ordered = [.. paths.OrderBy(path => path.Date)];

        DateTime? cutoff = ordered.SkipLast(normalizedRetainedCountLimit).LastOrDefault()?.Date;

        if (cutoff is null)
            return [];

        return [.. ordered.Where(path => path.Date <= cutoff.Value).Select(path => path.Path)];
    }
}
