using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkPlanningService
{
    MediaBackupChunkPlanningResult Plan(
        int totalPathCount,
        int totalChunkCount,
        int backupIncrease,
        int configuredIncrease,
        IEnumerable<int> existingChunkIds
    );
}
