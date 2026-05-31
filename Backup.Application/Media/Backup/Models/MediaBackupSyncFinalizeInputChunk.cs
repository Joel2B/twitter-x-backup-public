namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupSyncFinalizeInputChunk
{
    public required int ChunkId { get; init; }
    public required IReadOnlyList<string> Paths { get; init; }
}
