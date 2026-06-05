using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupPathArchiveMetadataProjectionService
    : IMediaBackupPathArchiveMetadataProjectionService
{
    public IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> BuildPathMetadataByPath(
        IEnumerable<string> paths,
        IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByArchivePath
    ) =>
        paths.ToDictionary(
            path => path,
            path =>
            {
                string archivePath = MediaBackupPathProjection.ToArchivePath(path);

                if (
                    !metadataByArchivePath.TryGetValue(
                        archivePath,
                        out MediaBackupChunkDataMetadata? metadata
                    )
                )
                    return new MediaBackupChunkDataMetadata();

                return new MediaBackupChunkDataMetadata
                {
                    FileSize = metadata.FileSize,
                    Crc32 = metadata.Crc32,
                };
            },
            StringComparer.Ordinal
        );
}
