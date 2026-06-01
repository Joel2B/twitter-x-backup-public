using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheConflictResolutionService
{
    MediaCacheConflictResolution Resolve(long? existingStreamSizeBytes, MediaCacheWritePlan writePlan);
}
