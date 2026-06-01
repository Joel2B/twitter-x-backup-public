using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckEvaluationService(
    IMediaCacheRecheckDecisionService mediaCacheRecheckDecisionService
) : IMediaCacheRecheckEvaluationService
{
    private readonly IMediaCacheRecheckDecisionService _mediaCacheRecheckDecisionService =
        mediaCacheRecheckDecisionService;

    public IReadOnlyList<MediaCacheRecheckEvaluation> Evaluate(
        IReadOnlyCollection<MediaCacheRecheckObservation> observations
    )
    {
        List<MediaCacheRecheckEvaluation> evaluations = [];

        foreach (MediaCacheRecheckObservation observation in observations)
        {
            MediaCacheRecheckApplyResult result = _mediaCacheRecheckDecisionService.Decide(observation);

            evaluations.Add(
                new MediaCacheRecheckEvaluation
                {
                    Path = observation.Path,
                    IsInvalid = result.IsInvalid,
                    ShouldRemove = result.ShouldRemove,
                    UpdatedEntryState = result.UpdatedEntryState,
                }
            );
        }

        return evaluations;
    }
}
