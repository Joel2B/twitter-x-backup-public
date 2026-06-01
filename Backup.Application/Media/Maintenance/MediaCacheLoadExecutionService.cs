using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheLoadExecutionService(
    IMediaCacheRecheckExecutionInputService mediaCacheRecheckExecutionInputService,
    IMediaCacheRecheckEvaluationService mediaCacheRecheckEvaluationService,
    IMediaCacheRecheckMutationPlanningService mediaCacheRecheckMutationPlanningService
) : IMediaCacheLoadExecutionService
{
    private readonly IMediaCacheRecheckExecutionInputService _mediaCacheRecheckExecutionInputService =
        mediaCacheRecheckExecutionInputService;
    private readonly IMediaCacheRecheckEvaluationService _mediaCacheRecheckEvaluationService =
        mediaCacheRecheckEvaluationService;
    private readonly IMediaCacheRecheckMutationPlanningService _mediaCacheRecheckMutationPlanningService =
        mediaCacheRecheckMutationPlanningService;

    public MediaCacheLoadExecutionResult Execute(
        IReadOnlyList<MediaCacheStoredEntry> entries,
        IReadOnlyCollection<string> existingCachePaths,
        Func<IReadOnlyList<MediaCacheRecheckProbeInput>, MediaCacheRecheckProbeExecutionResult> probe
    )
    {
        MediaCacheRecheckExecutionInput input = _mediaCacheRecheckExecutionInputService.BuildInputs(
            entries
        );
        MediaCacheRecheckProbeExecutionResult probeResult = probe(input.ProbeInputs);
        IReadOnlyList<MediaCacheRecheckEvaluation> evaluations =
            _mediaCacheRecheckEvaluationService.Evaluate(probeResult.Observations);
        IReadOnlyList<MediaCacheRecheckMutation> mutations =
            _mediaCacheRecheckMutationPlanningService.Plan(
                evaluations,
                existingCachePaths.ToHashSet(StringComparer.OrdinalIgnoreCase)
            );

        return new MediaCacheLoadExecutionResult
        {
            RecheckPaths = input.RecheckPaths,
            Mutations = mutations,
            FailedProbePaths = probeResult.FailedPaths,
        };
    }
}
