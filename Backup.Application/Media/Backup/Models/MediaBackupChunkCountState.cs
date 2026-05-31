namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkCountState
{
    public required int ChunkId { get; init; }
    public required int PathCount { get; init; }
}
