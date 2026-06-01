using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckMutationApplyPlanService
{
    MediaCacheRecheckMutationApplyPlan BuildPlan(
        IReadOnlyList<MediaCacheRecheckMutation> mutations
    );
}
