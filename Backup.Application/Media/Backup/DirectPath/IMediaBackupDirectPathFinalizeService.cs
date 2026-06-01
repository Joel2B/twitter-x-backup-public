using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupDirectPathFinalizeService
{
    MediaBackupDirectPathFinalizeResult Finalize(
        IEnumerable<string> pathsInChunks,
        IEnumerable<string> directPaths
    );
}
