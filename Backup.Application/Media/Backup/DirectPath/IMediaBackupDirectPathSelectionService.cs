using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupDirectPathSelectionService
{
    MediaBackupDirectPathSelectionResult Select(
        IEnumerable<string> pathsInChunks,
        IEnumerable<string> directPathCandidates
    );
}
