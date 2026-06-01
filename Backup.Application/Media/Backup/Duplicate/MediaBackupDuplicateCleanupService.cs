using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDuplicateCleanupService : IMediaBackupDuplicateCleanupService
{
    public MediaBackupDuplicateCleanupPlan BuildPlan(
        IReadOnlyList<MediaPathDuplicateGroup> duplicateGroups
    )
    {
        List<MediaBackupDuplicateCleanupOperation> operations = [];

        foreach (MediaPathDuplicateGroup group in duplicateGroups)
        {
            string? entry = group.Entries.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(entry))
                continue;

            operations.Add(
                new MediaBackupDuplicateCleanupOperation
                {
                    EntryPath = entry,
                    RemoveDuplicateEntries = true,
                }
            );
        }

        int removedPathCount =
            duplicateGroups.Sum(group => group.Entries.Count) - duplicateGroups.Count;

        return new MediaBackupDuplicateCleanupPlan
        {
            Operations = operations,
            RemovedPathCount = removedPathCount,
        };
    }
}
