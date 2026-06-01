using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupDuplicateChunkOrchestrationService
{
    MediaBackupDuplicateChunkExecutionPlan BuildExecutionPlan(
        MediaBackupDuplicateCheckPlan plan,
        int extraPreviewLimit
    );
    int UpdateStorageCount(int currentStorageCount, int storageEntriesRead, int removedExtrasCount);
}
