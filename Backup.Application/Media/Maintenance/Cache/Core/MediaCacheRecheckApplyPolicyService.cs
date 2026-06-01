using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckApplyPolicyService(
    IMediaCacheEntryStateFactoryService mediaCacheEntryStateFactoryService
) : IMediaCacheRecheckApplyPolicyService
{
    private readonly IMediaCacheEntryStateFactoryService _mediaCacheEntryStateFactoryService =
        mediaCacheEntryStateFactoryService;

    public MediaCacheRecheckApplyResult Apply(string path, MediaCacheRecheckResult decision)
    {
        if (decision.IsInvalid)
            return new MediaCacheRecheckApplyResult { IsInvalid = true };

        if (decision.ShouldRemove)
            return new MediaCacheRecheckApplyResult { ShouldRemove = true };

        if (!decision.ShouldUpdate || decision.PartitionId is null)
            return new MediaCacheRecheckApplyResult();

        MediaCacheEntryState updated = _mediaCacheEntryStateFactoryService.Create(
            path,
            decision.PartitionId.Value,
            decision.StreamSizeBytes,
            decision.FileSizeBytes
        );

        return new MediaCacheRecheckApplyResult { UpdatedEntryState = updated };
    }
}
