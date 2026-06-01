using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupPathArchiveMetadataProjectionService(
    IMediaBackupPathProjectionService mediaBackupPathProjectionService
) : IMediaBackupPathArchiveMetadataProjectionService
{
    private readonly IMediaBackupPathProjectionService _mediaBackupPathProjectionService =
        mediaBackupPathProjectionService;

    public IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> BuildPathMetadataByPath(
        IEnumerable<string> paths,
        IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByArchivePath
    ) =>
        paths.ToDictionary(
            path => path,
            path =>
            {
                string archivePath = _mediaBackupPathProjectionService.ToArchivePath(path);

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
