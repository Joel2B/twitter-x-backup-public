using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupApplyChunkPlanningService
{
    MediaBackupApplyChunkPlan Plan(
        IReadOnlyCollection<MediaBackupApplyChunkPathState> chunkPaths,
        ISet<string> storagePaths,
        IEnumerable<int> existingChunkIds,
        int chunkId
    );
}
