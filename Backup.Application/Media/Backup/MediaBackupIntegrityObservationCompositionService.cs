using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupIntegrityObservationCompositionService
    : IMediaBackupIntegrityObservationCompositionService
{
    public IReadOnlyList<MediaBackupIntegrityObservation> BuildObservations(
        IEnumerable<MediaBackupIntegrityObservationInput> inputs
    ) =>
        inputs.Select(input => new MediaBackupIntegrityObservation
            {
                ChunkId = input.ChunkId,
                Path = input.Path,
                ExpectedFileSize = input.ExpectedFileSize,
                ActualFileSize = input.ActualFileSize,
                ExpectedCrc32 = input.ExpectedCrc32,
                ActualCrc32 = input.ActualCrc32,
            })
            .ToList();

    public IReadOnlyList<MediaBackupChunkPathMetadataState> BuildPathMetadataStates(
        IEnumerable<MediaBackupChunkPathMetadataInput> inputs
    ) =>
        inputs.Select(input => new MediaBackupChunkPathMetadataState
            {
                Path = input.Path,
                FileSize = input.FileSize,
                Crc32 = input.Crc32,
            })
            .ToList();
}
