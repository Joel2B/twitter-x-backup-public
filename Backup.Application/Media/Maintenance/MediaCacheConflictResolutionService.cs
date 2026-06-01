using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheConflictResolutionService(
    IMediaCacheWritePolicyService mediaCacheWritePolicyService
) : IMediaCacheConflictResolutionService
{
    private readonly IMediaCacheWritePolicyService _mediaCacheWritePolicyService =
        mediaCacheWritePolicyService;

    public MediaCacheConflictResolution Resolve(long? existingStreamSizeBytes, MediaCacheWritePlan writePlan) =>
        _mediaCacheWritePolicyService.HasConflict(existingStreamSizeBytes, writePlan)
            ? new MediaCacheConflictResolution { Action = MediaCacheConflictAction.ThrowConflict }
            : new MediaCacheConflictResolution { Action = MediaCacheConflictAction.KeepExisting };
}
