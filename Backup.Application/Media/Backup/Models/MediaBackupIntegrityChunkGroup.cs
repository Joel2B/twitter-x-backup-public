namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupIntegrityChunkGroup
{
    public required int ChunkId { get; init; }

    public required IReadOnlyList<string> Paths { get; init; }
}
