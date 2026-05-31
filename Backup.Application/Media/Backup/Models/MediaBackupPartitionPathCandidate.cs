namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupPartitionPathCandidate
{
    public required string Type { get; init; }
    public required string RootPath { get; init; }
}
