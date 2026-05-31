using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupIntegrityChunkRefreshService
{
    MediaBackupIntegrityChunkApplyResult Refresh(
        IEnumerable<string> changedPaths,
        IEnumerable<string> chunkPaths,
        IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByPath,
        IEnumerable<MediaBackupChunkEntryState> entries
    );
}
