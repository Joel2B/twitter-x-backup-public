namespace Backup.Application.Posts.Models;

public sealed class PostHistoryPrunePlan
{
    public int NormalizedKeepDays { get; init; }
    public int NormalizedKeepCount { get; init; }
    public int DistinctDayCount { get; init; }
    public IReadOnlyList<string> PathsToRemove { get; init; } = Array.Empty<string>();
}
