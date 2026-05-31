namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkSyncChunkPlan
{
    public required int ChunkId { get; init; }

    public required IReadOnlyList<string> PathsToRemove { get; init; }
}
