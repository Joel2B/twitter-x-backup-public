using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkRuntimeCompositionService
{
    IReadOnlyList<MediaBackupChunkState> BuildChunkStates(
        IEnumerable<MediaBackupChunkStateInput> inputs
    );

    IReadOnlyList<MediaBackupChunkPathsState> BuildChunkPathStates(
        IEnumerable<MediaBackupChunkPathsInput> inputs
    );

    IReadOnlyList<MediaBackupChunkReportObservation> BuildChunkReportObservations(
        IEnumerable<MediaBackupChunkReportObservationInput> inputs
    );
}
