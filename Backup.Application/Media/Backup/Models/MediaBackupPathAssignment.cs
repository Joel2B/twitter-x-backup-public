namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupPathAssignment
{
    public required int ChunkId { get; init; }

    public required string OriginalPath { get; init; }

    public required string CachePath { get; init; }
}
