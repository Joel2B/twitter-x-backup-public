using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkMetadataRefreshPlanningService(
    IMediaBackupChunkMetadataPolicyService mediaBackupChunkMetadataPolicyService
) : IMediaBackupChunkMetadataRefreshPlanningService
{
    private readonly IMediaBackupChunkMetadataPolicyService _mediaBackupChunkMetadataPolicyService =
        mediaBackupChunkMetadataPolicyService;

    public MediaBackupChunkMetadataRefreshPlan Plan(
        IEnumerable<MediaBackupChunkMetadataRefreshCandidate> candidates
    )
    {
        List<MediaBackupChunkMetadataRefreshUpdate> updates = [];

        foreach (MediaBackupChunkMetadataRefreshCandidate item in candidates)
        {
            if (!item.HasEntry)
                continue;

            MediaBackupChunkDataMetadata merged = _mediaBackupChunkMetadataPolicyService.Merge(
                item.Current,
                item.Entry
            );

            updates.Add(
                new MediaBackupChunkMetadataRefreshUpdate { Path = item.Path, Metadata = merged }
            );
        }

        return new MediaBackupChunkMetadataRefreshPlan { Updates = updates };
    }
}
