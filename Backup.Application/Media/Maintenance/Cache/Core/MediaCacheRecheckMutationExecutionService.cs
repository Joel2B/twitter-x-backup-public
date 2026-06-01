using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckMutationExecutionService(
    IMediaCacheRecheckMutationApplyPlanService mediaCacheRecheckMutationApplyPlanService,
    IMediaCacheRecheckMutationApplySelectionService mediaCacheRecheckMutationApplySelectionService
) : IMediaCacheRecheckMutationExecutionService
{
    private readonly IMediaCacheRecheckMutationApplyPlanService _mediaCacheRecheckMutationApplyPlanService =
        mediaCacheRecheckMutationApplyPlanService;
    private readonly IMediaCacheRecheckMutationApplySelectionService _mediaCacheRecheckMutationApplySelectionService =
        mediaCacheRecheckMutationApplySelectionService;

    public MediaCacheRecheckMutationApplySelection Execute(
        IReadOnlyList<MediaCacheRecheckMutation> mutations,
        IReadOnlySet<string> existingPaths
    )
    {
        MediaCacheRecheckMutationApplyPlan plan =
            _mediaCacheRecheckMutationApplyPlanService.BuildPlan(mutations);

        return _mediaCacheRecheckMutationApplySelectionService.Select(plan, existingPaths);
    }
}
