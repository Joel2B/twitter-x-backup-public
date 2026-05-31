namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupDuplicateCleanupOperation
{
    public required string EntryPath { get; init; }

    public required bool RemoveDuplicateEntries { get; init; }
}
