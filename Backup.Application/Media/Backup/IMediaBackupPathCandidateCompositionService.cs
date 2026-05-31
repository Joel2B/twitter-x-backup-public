using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupPathCandidateCompositionService
{
    IReadOnlyList<MediaBackupPathCandidate> Compose(
        IEnumerable<MediaBackupPathCacheObservation> observations,
        ISet<string> assignedCachePaths
    );
}
