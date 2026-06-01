using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkSyncPlanningService
{
    MediaBackupChunkSyncPlan Plan(
        IReadOnlyList<MediaBackupChunkPathsState> chunks,
        IEnumerable<string> pathsInBoth
    );
}
