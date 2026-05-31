using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkSnapshotCompositionService
{
    IReadOnlyList<MediaBackupChunkCountState> BuildChunkCountStates(
        IEnumerable<MediaBackupChunkPathsState> chunks
    );

    MediaBackupChunkPathMaps BuildPathMaps(
        IEnumerable<MediaBackupChunkPathsState> before,
        IEnumerable<MediaBackupChunkPathsState> after
    );
}
