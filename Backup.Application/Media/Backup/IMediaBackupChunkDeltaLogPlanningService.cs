using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkDeltaLogPlanningService
{
    MediaBackupChunkDeltaLogPlan Plan(
        IEnumerable<MediaBackupChunkDeltaLogInput> inputs,
        int totalAddedPaths,
        int addedPathCount
    );
}
