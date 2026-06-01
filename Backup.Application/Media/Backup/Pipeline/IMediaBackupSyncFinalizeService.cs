using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupSyncFinalizeService
{
    MediaBackupSyncFinalizeResult Finalize(
        IReadOnlyList<MediaBackupSyncFinalizeInputChunk> chunks,
        IReadOnlyList<string> pathsInBoth,
        IEnumerable<string> currentDirectPaths
    );
}
