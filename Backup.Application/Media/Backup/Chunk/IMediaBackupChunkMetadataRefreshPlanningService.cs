using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkMetadataRefreshPlanningService
{
    MediaBackupChunkMetadataRefreshPlan Plan(
        IEnumerable<MediaBackupChunkMetadataRefreshCandidate> candidates
    );
}
