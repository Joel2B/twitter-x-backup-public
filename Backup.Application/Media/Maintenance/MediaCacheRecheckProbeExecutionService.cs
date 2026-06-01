using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckProbeExecutionService(
    IMediaCacheRecheckObservationCompositionService mediaCacheRecheckObservationCompositionService
) : IMediaCacheRecheckProbeExecutionService
{
    private readonly IMediaCacheRecheckObservationCompositionService _mediaCacheRecheckObservationCompositionService =
        mediaCacheRecheckObservationCompositionService;

    public MediaCacheRecheckProbeExecutionResult Execute(
        IReadOnlyList<MediaCacheRecheckProbeInput> probeInputs,
        Func<MediaCacheRecheckProbeInput, MediaCacheRecheckProbeOutcome> probe
    )
    {
        List<MediaCacheRecheckProbeOutcome> outcomes = [];
        List<string> failedPaths = [];

        foreach (MediaCacheRecheckProbeInput probeInput in probeInputs)
        {
            try
            {
                outcomes.Add(probe(probeInput));
            }
            catch
            {
                failedPaths.Add(probeInput.Path);
            }
        }

        IReadOnlyList<MediaCacheRecheckObservation> observations =
            _mediaCacheRecheckObservationCompositionService.ToObservations(outcomes);

        return new MediaCacheRecheckProbeExecutionResult
        {
            Observations = observations,
            FailedPaths = failedPaths,
        };
    }
}
