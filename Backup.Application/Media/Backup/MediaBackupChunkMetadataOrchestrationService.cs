using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkMetadataOrchestrationService(
    IMediaBackupChunkMetadataPolicyService mediaBackupChunkMetadataPolicyService,
    IMediaBackupChunkMetadataRefreshPlanningService mediaBackupChunkMetadataRefreshPlanningService
) : IMediaBackupChunkMetadataOrchestrationService
{
    private readonly IMediaBackupChunkMetadataPolicyService _mediaBackupChunkMetadataPolicyService =
        mediaBackupChunkMetadataPolicyService;
    private readonly IMediaBackupChunkMetadataRefreshPlanningService
        _mediaBackupChunkMetadataRefreshPlanningService = mediaBackupChunkMetadataRefreshPlanningService;

    public bool RequiresRefresh(IEnumerable<MediaBackupChunkPathMetadataState> items) =>
        _mediaBackupChunkMetadataPolicyService.RequiresRefresh(
            items.Select(item => new MediaBackupChunkDataMetadata
            {
                FileSize = item.FileSize,
                Crc32 = item.Crc32,
            })
        );

    public IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> PlanUpdates(
        IEnumerable<MediaBackupChunkMetadataObservation> observations
    )
    {
        List<MediaBackupChunkMetadataRefreshCandidate> candidates = observations
            .Select(item => new MediaBackupChunkMetadataRefreshCandidate
            {
                Path = item.Path,
                HasEntry = item.HasEntry,
                Current = new MediaBackupChunkDataMetadata
                {
                    FileSize = item.CurrentFileSize,
                    Crc32 = item.CurrentCrc32,
                },
                Entry = new MediaBackupChunkDataMetadata
                {
                    FileSize = item.EntryFileSize,
                    Crc32 = item.EntryCrc32,
                },
            })
            .ToList();

        MediaBackupChunkMetadataRefreshPlan plan =
            _mediaBackupChunkMetadataRefreshPlanningService.Plan(candidates);

        return plan.Updates.ToDictionary(item => item.Path, item => item.Metadata);
    }

    public IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> BuildPathMetadataMap(
        IEnumerable<MediaBackupChunkPathMetadataState> items
    ) =>
        items.ToDictionary(
            item => item.Path,
            item =>
                new MediaBackupChunkDataMetadata
                {
                    FileSize = item.FileSize,
                    Crc32 = item.Crc32,
                },
            StringComparer.Ordinal
        );
}
