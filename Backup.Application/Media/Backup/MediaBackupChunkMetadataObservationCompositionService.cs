using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkMetadataObservationCompositionService
    : IMediaBackupChunkMetadataObservationCompositionService
{
    public IReadOnlyList<MediaBackupChunkMetadataObservation> BuildObservations(
        IEnumerable<MediaBackupChunkMetadataObservationInput> inputs
    ) =>
        inputs.Select(input => new MediaBackupChunkMetadataObservation
            {
                Path = input.Path,
                HasEntry = input.HasEntry,
                CurrentFileSize = input.CurrentFileSize,
                CurrentCrc32 = input.CurrentCrc32,
                EntryFileSize = input.EntryFileSize,
                EntryCrc32 = input.EntryCrc32,
            })
            .ToList();
}
