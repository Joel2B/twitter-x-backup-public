using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckPlanningService(
    IMediaCacheStoredEntryProjectionService mediaCacheStoredEntryProjectionService,
    IMediaCacheRecheckOrchestrationService mediaCacheRecheckOrchestrationService
) : IMediaCacheRecheckPlanningService
{
    private readonly IMediaCacheStoredEntryProjectionService _mediaCacheStoredEntryProjectionService =
        mediaCacheStoredEntryProjectionService;
    private readonly IMediaCacheRecheckOrchestrationService _mediaCacheRecheckOrchestrationService =
        mediaCacheRecheckOrchestrationService;

    public IReadOnlyCollection<string> SelectPathsToRecheck(
        IReadOnlyCollection<MediaCacheStoredEntry> entries
    )
    {
        IReadOnlyList<MediaCacheRecheckCandidate> candidates =
            _mediaCacheStoredEntryProjectionService.ToRecheckCandidates(entries);
        return _mediaCacheRecheckOrchestrationService.SelectRecheckPaths(candidates);
    }
}
