using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkMetadataObservationCompositionService
{
    IReadOnlyList<MediaBackupChunkMetadataObservation> BuildObservations(
        IEnumerable<MediaBackupChunkMetadataObservationInput> inputs
    );
}
