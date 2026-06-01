using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheWritePolicyService
{
    MediaCacheWritePlan BuildWritePlan(string path, int partitionId, long streamSizeBytes);
    bool HasConflict(long? existingStreamSizeBytes, MediaCacheWritePlan plan);
}
