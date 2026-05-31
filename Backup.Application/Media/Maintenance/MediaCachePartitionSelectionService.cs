using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCachePartitionSelectionService : IMediaCachePartitionSelectionService
{
    public MediaCachePartitionSelection Select(
        long streamSizeBytes,
        long heavyThresholdBytes,
        int? cachedPartitionId
    )
    {
        bool useHeavyPartition = streamSizeBytes > 0 && streamSizeBytes >= heavyThresholdBytes;

        return new MediaCachePartitionSelection
        {
            UseHeavyPartition = useHeavyPartition,
            PreferredPartitionId = useHeavyPartition ? null : cachedPartitionId,
            RequestedSizeBytes = streamSizeBytes,
        };
    }
}
