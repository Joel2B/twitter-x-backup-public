using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupDuplicateCheckPlanningService
{
    MediaBackupDuplicateCheckPlan Plan(
        IReadOnlyList<string> memoryPaths,
        IReadOnlyList<string> storagePaths
    );
}
