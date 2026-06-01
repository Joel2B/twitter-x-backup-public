namespace Backup.Application.Posts.Models;

public sealed class PostSnapshotVerificationDecision
{
    public bool ShouldInspectHistoryFile { get; init; }
    public string SnapshotFileName { get; init; } = string.Empty;
    public string HistoryDirectoryName { get; init; } = string.Empty;
    public string HistoryFilePath { get; init; } = string.Empty;
}
