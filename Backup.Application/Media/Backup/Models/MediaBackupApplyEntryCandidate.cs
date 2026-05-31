namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupApplyEntryCandidate
{
    public required string SourcePath { get; init; }
    public required string ArchivePath { get; init; }
    public required bool HasHash { get; init; }
}
