namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupPathCacheObservation
{
    public required string OriginalPath { get; init; }
    public required bool CacheExists { get; init; }
    public required string CachePath { get; init; }
    public long? FileSizeBytes { get; init; }
}
