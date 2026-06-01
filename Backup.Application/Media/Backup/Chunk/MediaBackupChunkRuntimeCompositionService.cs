using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkRuntimeCompositionService
    : IMediaBackupChunkRuntimeCompositionService
{
    public IReadOnlyList<MediaBackupChunkState> BuildChunkStates(
        IEnumerable<MediaBackupChunkStateInput> inputs
    ) =>
        inputs
            .Select(input => new MediaBackupChunkState
            {
                Id = input.Id,
                PathCount = input.PathCount,
                SizeBytes = input.SizeBytes,
            })
            .ToList();

    public IReadOnlyList<MediaBackupChunkPathsState> BuildChunkPathStates(
        IEnumerable<MediaBackupChunkPathsInput> inputs
    ) =>
        inputs
            .Select(input => new MediaBackupChunkPathsState { Id = input.Id, Paths = input.Paths })
            .ToList();

    public IReadOnlyList<MediaBackupChunkReportObservation> BuildChunkReportObservations(
        IEnumerable<MediaBackupChunkReportObservationInput> inputs
    ) =>
        inputs
            .Select(input => new MediaBackupChunkReportObservation
            {
                ChunkId = input.ChunkId,
                PathCount = input.PathCount,
                SizeBytes = input.SizeBytes,
            })
            .ToList();
}
