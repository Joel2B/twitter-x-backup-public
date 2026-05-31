namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupDirectPathObservationInput
{
    public required string Path { get; init; }

    public required bool CacheExists { get; init; }

    public string? CachePath { get; init; }

    public long? FileSizeBytes { get; init; }

    public required bool SourceExists { get; init; }

    public required bool TargetExists { get; init; }

    public required long MaxPathSizeBytes { get; init; }
}
