using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckPlanningService(
    IMediaCacheRecheckOrchestrationService mediaCacheRecheckOrchestrationService
) : IMediaCacheRecheckPlanningService
{
    private readonly IMediaCacheRecheckOrchestrationService _mediaCacheRecheckOrchestrationService =
        mediaCacheRecheckOrchestrationService;

    public IReadOnlyCollection<string> SelectPathsToRecheck(
        IReadOnlyCollection<MediaCacheStoredEntry> entries
    )
    {
        IReadOnlyList<MediaCacheRecheckCandidate> candidates = entries
            .Select(entry => new MediaCacheRecheckCandidate
            {
                Path = entry.Path,
                StreamSizeBytes = entry.StreamSizeBytes,
                FileSizeBytes = entry.FileSizeBytes,
            })
            .ToList();

        return _mediaCacheRecheckOrchestrationService.SelectRecheckPaths(candidates);
    }
}
