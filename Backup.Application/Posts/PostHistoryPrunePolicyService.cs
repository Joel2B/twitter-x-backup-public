using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostHistoryPrunePolicyService : IPostHistoryPrunePolicyService
{
    public IReadOnlyList<string> GetPathsToRemove(
        IReadOnlyList<PostHistoryPath> paths,
        int keepDays,
        int keepCount
    )
    {
        if (paths.Count == 0)
            return [];

        int normalizedKeepDays = Math.Max(1, keepDays);
        int normalizedKeepCount = Math.Max(0, keepCount);

        List<DayGroup> dayGroups = paths
            .GroupBy(path => path.Date.Date)
            .Select(group => new DayGroup(
                group.Key,
                group.OrderBy(path => path.Date).Select(path => path.Path).ToList()
            ))
            .OrderBy(group => group.Date)
            .ToList();

        HashSet<DateTime> keepDates =
        [
            .. dayGroups
                .OrderByDescending(group => group.Date)
                .Take(normalizedKeepDays)
                .Select(group => group.Date),
        ];

        List<string> remove = [];

        foreach (DayGroup day in dayGroups)
        {
            if (!keepDates.Contains(day.Date))
            {
                remove.AddRange(day.Paths);
                continue;
            }

            int removeCount = Math.Max(0, day.Paths.Count - normalizedKeepCount);
            remove.AddRange(day.Paths.Take(removeCount));
        }

        return remove;
    }

    private sealed record DayGroup(DateTime Date, List<string> Paths);
}
