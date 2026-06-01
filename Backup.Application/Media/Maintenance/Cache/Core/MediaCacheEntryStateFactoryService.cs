using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheEntryStateFactoryService : IMediaCacheEntryStateFactoryService
{
    public MediaCacheEntryState Create(
        string normalizedPath,
        int partitionId,
        long? streamSizeBytes = null,
        long? fileSizeBytes = null
    ) =>
        new()
        {
            Path = normalizedPath,
            PartitionId = partitionId,
            StreamSizeBytes = streamSizeBytes,
            FileSizeBytes = fileSizeBytes,
        };

    public bool HasStreamSizeConflict(long? existingStreamSizeBytes, long? incomingStreamSizeBytes)
    {
        if (existingStreamSizeBytes is null || incomingStreamSizeBytes is null)
            return false;

        return existingStreamSizeBytes.Value != incomingStreamSizeBytes.Value;
    }
}
