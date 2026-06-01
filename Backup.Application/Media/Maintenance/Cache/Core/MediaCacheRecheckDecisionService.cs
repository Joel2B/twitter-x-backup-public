using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckDecisionService(
    IMediaCacheRecheckOrchestrationService mediaCacheRecheckOrchestrationService,
    IMediaCacheRecheckApplyPolicyService mediaCacheRecheckApplyPolicyService
) : IMediaCacheRecheckDecisionService
{
    private readonly IMediaCacheRecheckOrchestrationService _mediaCacheRecheckOrchestrationService =
        mediaCacheRecheckOrchestrationService;
    private readonly IMediaCacheRecheckApplyPolicyService _mediaCacheRecheckApplyPolicyService =
        mediaCacheRecheckApplyPolicyService;

    public MediaCacheRecheckApplyResult Decide(MediaCacheRecheckObservation observation)
    {
        MediaCacheRecheckResult decision = _mediaCacheRecheckOrchestrationService.Evaluate(
            observation
        );
        return _mediaCacheRecheckApplyPolicyService.Apply(observation.Path, decision);
    }
}
