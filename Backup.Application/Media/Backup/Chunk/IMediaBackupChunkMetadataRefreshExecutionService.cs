using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkMetadataRefreshExecutionService
{
    bool RequiresRefresh(IEnumerable<MediaBackupChunkEntryState> entries);

    MediaBackupChunkMetadataRefreshExecutionResult Refresh(
        IEnumerable<MediaBackupChunkEntryState> entries,
        IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> archiveMetadataByPath
    );
}
