using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupPathArchiveMetadataProjectionService
{
    IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> BuildPathMetadataByPath(
        IEnumerable<string> paths,
        IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByArchivePath
    );
}
