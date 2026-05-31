namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupIntegrityPathChange
{
    public required int ChunkId { get; init; }

    public required string Path { get; init; }
}
