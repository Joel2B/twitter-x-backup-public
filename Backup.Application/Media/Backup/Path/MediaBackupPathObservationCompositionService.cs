using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupPathObservationCompositionService
    : IMediaBackupPathObservationCompositionService
{
    public IReadOnlyList<MediaBackupPathCacheObservation> BuildPathCacheObservations(
        IEnumerable<MediaBackupPathCacheObservationInput> inputs
    ) =>
        inputs
            .Select(input => new MediaBackupPathCacheObservation
            {
                OriginalPath = input.OriginalPath,
                CacheExists = input.CacheExists,
                CachePath = input.CachePath ?? string.Empty,
                FileSizeBytes = input.FileSizeBytes,
            })
            .ToList();
}
