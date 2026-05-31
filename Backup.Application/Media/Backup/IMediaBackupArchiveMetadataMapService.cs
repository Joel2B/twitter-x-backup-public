using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupArchiveMetadataMapService
{
    IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> BuildByArchivePath(
        IEnumerable<MediaBackupArchiveMetadataInput> entries
    );
}
