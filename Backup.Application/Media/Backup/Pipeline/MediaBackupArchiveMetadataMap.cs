using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public static class MediaBackupArchiveMetadataMap
{
    public static IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> BuildByArchivePath(
        IEnumerable<MediaBackupArchiveMetadataInput> entries
    ) =>
        entries.ToDictionary(
            item => item.ArchivePath,
            item => new MediaBackupChunkDataMetadata
            {
                FileSize = item.FileSize,
                Crc32 = item.Crc32,
            },
            StringComparer.Ordinal
        );
}
