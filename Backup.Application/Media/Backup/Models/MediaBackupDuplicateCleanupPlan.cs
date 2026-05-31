namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupDuplicateCleanupPlan
{
    public required IReadOnlyList<MediaBackupDuplicateCleanupOperation> Operations { get; init; }

    public required int RemovedPathCount { get; init; }
}
