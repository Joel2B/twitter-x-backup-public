namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupSyncFinalizeResult
{
    public required MediaBackupChunkSyncPlan Plan { get; init; }
    public required IReadOnlyList<string> MergedDirectPaths { get; init; }
}
