using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheWritePolicyService(
    IMediaCacheEntryPathPolicyService mediaCacheEntryPathPolicyService,
    IMediaCacheEntryStateFactoryService mediaCacheEntryStateFactoryService
) : IMediaCacheWritePolicyService
{
    private readonly IMediaCacheEntryPathPolicyService _mediaCacheEntryPathPolicyService =
        mediaCacheEntryPathPolicyService;
    private readonly IMediaCacheEntryStateFactoryService _mediaCacheEntryStateFactoryService =
        mediaCacheEntryStateFactoryService;

    public MediaCacheWritePlan BuildWritePlan(string path, int partitionId, long streamSizeBytes)
    {
        string cacheKey = _mediaCacheEntryPathPolicyService.NormalizeForCacheKey(path);
        MediaCacheEntryState entryState = _mediaCacheEntryStateFactoryService.Create(
            cacheKey,
            partitionId,
            streamSizeBytes
        );

        return new MediaCacheWritePlan { CacheKey = cacheKey, EntryState = entryState };
    }

    public bool HasConflict(long? existingStreamSizeBytes, MediaCacheWritePlan plan) =>
        _mediaCacheEntryStateFactoryService.HasStreamSizeConflict(
            existingStreamSizeBytes,
            plan.EntryState.StreamSizeBytes
        );
}
