using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCachePartitionSelectionService
{
    MediaCachePartitionSelection Select(
        long streamSizeBytes,
        long heavyThresholdBytes,
        int? cachedPartitionId
    );
}
