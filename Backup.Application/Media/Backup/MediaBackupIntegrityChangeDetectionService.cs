using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupIntegrityChangeDetectionService(
    IMediaBackupIntegrityPlanningService mediaBackupIntegrityPlanningService
) : IMediaBackupIntegrityChangeDetectionService
{
    private readonly IMediaBackupIntegrityPlanningService _mediaBackupIntegrityPlanningService =
        mediaBackupIntegrityPlanningService;

    public IReadOnlyList<MediaBackupIntegrityChange> Detect(
        IEnumerable<MediaBackupIntegrityObservation> observations
    )
    {
        List<MediaBackupIntegrityChange> changes = [];

        foreach (MediaBackupIntegrityObservation observation in observations)
        {
            if (
                !_mediaBackupIntegrityPlanningService.HasChange(
                    observation.ExpectedFileSize,
                    observation.ActualFileSize,
                    observation.ExpectedCrc32,
                    observation.ActualCrc32
                )
            )
                continue;

            changes.Add(
                new MediaBackupIntegrityChange
                {
                    ChunkId = observation.ChunkId,
                    Path = observation.Path,
                    ExpectedFileSize = observation.ExpectedFileSize,
                    ActualFileSize = observation.ActualFileSize,
                    ExpectedCrc32 = observation.ExpectedCrc32,
                    ActualCrc32 = observation.ActualCrc32,
                }
            );
        }

        return changes;
    }
}
