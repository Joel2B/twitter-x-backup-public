using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkMetadataPolicyService : IMediaBackupChunkMetadataPolicyService
{
    public bool RequiresRefresh(IEnumerable<MediaBackupChunkDataMetadata> metadata) =>
        metadata.Any(item => item.FileSize is null || item.FileSize == 0 || item.Crc32 is null);

    public MediaBackupChunkDataMetadata Merge(
        MediaBackupChunkDataMetadata current,
        MediaBackupChunkDataMetadata fromArchive
    ) =>
        new()
        {
            FileSize = current.FileSize ?? fromArchive.FileSize,
            Crc32 = current.Crc32 ?? fromArchive.Crc32,
        };
}
