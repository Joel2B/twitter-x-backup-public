namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupPathCacheObservationInput
{
    public required string OriginalPath { get; init; }

    public required bool CacheExists { get; init; }

    public string? CachePath { get; init; }

    public long? FileSizeBytes { get; init; }
}
