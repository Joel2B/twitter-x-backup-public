using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupDuplicateChunkExecutionService
{
    MediaBackupDuplicateChunkExecutionResult Execute(
        IEnumerable<string> chunkPaths,
        IEnumerable<string> storageArchivePaths,
        int extraPreviewLimit
    );
}
