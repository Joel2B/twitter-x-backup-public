using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheEntryStateFactoryService
{
    MediaCacheEntryState Create(
        string normalizedPath,
        int partitionId,
        long? streamSizeBytes = null,
        long? fileSizeBytes = null
    );
    bool HasStreamSizeConflict(long? existingStreamSizeBytes, long? incomingStreamSizeBytes);
}
