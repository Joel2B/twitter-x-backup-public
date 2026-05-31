using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkMetadataOrchestrationService
{
    bool RequiresRefresh(IEnumerable<MediaBackupChunkPathMetadataState> items);
    IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> PlanUpdates(
        IEnumerable<MediaBackupChunkMetadataObservation> observations
    );
    IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> BuildPathMetadataMap(
        IEnumerable<MediaBackupChunkPathMetadataState> items
    );
}
