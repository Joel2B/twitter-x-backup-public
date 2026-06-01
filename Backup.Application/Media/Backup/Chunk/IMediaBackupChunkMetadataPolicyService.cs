using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkMetadataPolicyService
{
    bool RequiresRefresh(IEnumerable<MediaBackupChunkDataMetadata> metadata);

    MediaBackupChunkDataMetadata Merge(
        MediaBackupChunkDataMetadata current,
        MediaBackupChunkDataMetadata fromArchive
    );
}
