namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkSyncPlan
{
    public required IReadOnlyList<MediaBackupChunkSyncChunkPlan> Chunks { get; init; }

    public required IReadOnlyList<string> DirectPathsToAdd { get; init; }
}
