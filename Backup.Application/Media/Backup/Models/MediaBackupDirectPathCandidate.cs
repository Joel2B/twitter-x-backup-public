namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupDirectPathCandidate
{
    public required string CachePath { get; init; }

    public long? FileSizeBytes { get; init; }

    public required bool TargetExists { get; init; }
}
