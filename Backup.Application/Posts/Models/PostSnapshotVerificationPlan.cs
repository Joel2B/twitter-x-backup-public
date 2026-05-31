namespace Backup.Application.Posts.Models;

public sealed class PostSnapshotVerificationPlan
{
    public bool ShouldCompareWithHistory { get; init; }
    public string HistoryDirectoryName { get; init; } = string.Empty;
    public string HistoryFilePath { get; init; } = string.Empty;
}
