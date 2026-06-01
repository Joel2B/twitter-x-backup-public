using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupIntegrityObservationCompositionService
{
    IReadOnlyList<MediaBackupIntegrityObservation> BuildChunkObservations(
        int chunkId,
        IEnumerable<MediaBackupChunkEntryState> entries,
        IReadOnlyDictionary<string, long?> actualFileSizeByPath,
        IReadOnlyDictionary<string, uint?> actualCrc32ByPath
    );

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
