using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupIntegrityObservationCompositionService
{
    IReadOnlyList<MediaBackupIntegrityObservation> BuildObservations(
        IEnumerable<MediaBackupIntegrityObservationInput> inputs
    );

    IReadOnlyList<MediaBackupChunkPathMetadataState> BuildPathMetadataStates(
        IEnumerable<MediaBackupChunkPathMetadataInput> inputs
    );

    IReadOnlyList<MediaBackupIntegrityPathChange> BuildPathChanges(
        IEnumerable<MediaBackupIntegrityChange> changes
    );
}
