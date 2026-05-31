using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupPathObservationCompositionService
{
    IReadOnlyList<MediaBackupPathCacheObservation> BuildPathCacheObservations(
        IEnumerable<MediaBackupPathCacheObservationInput> inputs
    );

    MediaBackupDirectPathCandidateObservation BuildDirectPathObservation(
        MediaBackupDirectPathObservationInput input
    );
}
