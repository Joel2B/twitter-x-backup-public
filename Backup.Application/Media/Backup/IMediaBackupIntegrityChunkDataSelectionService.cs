using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupIntegrityChunkDataSelectionService
{
    MediaBackupIntegrityChunkDataSelectionResult Select(
        IEnumerable<string> changedPaths,
        IEnumerable<string> chunkPaths
    );
}
