using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupApplyFinalizeService
{
    MediaBackupApplyFinalizePlan Plan(
        IEnumerable<string> memoryPaths,
        IEnumerable<string> storagePaths,
        IEnumerable<int> existingChunkIds,
        int chunkId
    );
}
