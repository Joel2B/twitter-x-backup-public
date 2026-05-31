namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupPathCandidate
{
    public required string OriginalPath { get; init; }

    public required string CachePath { get; init; }

    public long? FileSizeBytes { get; init; }

    public required bool IsAlreadyAssigned { get; init; }
}
