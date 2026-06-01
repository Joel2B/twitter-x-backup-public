using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckExecutionInputService(
    IMediaCacheRecheckPlanningService mediaCacheRecheckPlanningService,
    IMediaCacheRecheckObservationCompositionService mediaCacheRecheckObservationCompositionService
) : IMediaCacheRecheckExecutionInputService
{
    private readonly IMediaCacheRecheckPlanningService _mediaCacheRecheckPlanningService =
        mediaCacheRecheckPlanningService;
    private readonly IMediaCacheRecheckObservationCompositionService _mediaCacheRecheckObservationCompositionService =
        mediaCacheRecheckObservationCompositionService;

    public MediaCacheRecheckExecutionInput BuildInputs(
        IReadOnlyList<MediaCacheStoredEntry> entries
    )
    {
        IReadOnlyCollection<string> recheckPaths =
            _mediaCacheRecheckPlanningService.SelectPathsToRecheck(entries);
        IReadOnlyList<MediaCacheRecheckProbeInput> probeInputs =
            _mediaCacheRecheckObservationCompositionService.BuildProbeInputs(recheckPaths, entries);

        return new MediaCacheRecheckExecutionInput
        {
            RecheckPaths = recheckPaths,
            ProbeInputs = probeInputs,
        };
    }
}
