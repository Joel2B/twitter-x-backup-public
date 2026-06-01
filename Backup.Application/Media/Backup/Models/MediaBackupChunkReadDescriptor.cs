namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkReadDescriptor
{
    public required int Index { get; init; }
    public required int ChunkId { get; init; }
}
