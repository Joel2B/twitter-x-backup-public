using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupDuplicateCleanupService
{
    MediaBackupDuplicateCleanupPlan BuildPlan(
        IReadOnlyList<MediaPathDuplicateGroup> duplicateGroups
    );
}
