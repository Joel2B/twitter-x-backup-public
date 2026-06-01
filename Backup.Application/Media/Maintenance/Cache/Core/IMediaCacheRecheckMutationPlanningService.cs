using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckMutationPlanningService
{
    IReadOnlyList<MediaCacheRecheckMutation> Plan(
        IReadOnlyCollection<MediaCacheRecheckEvaluation> evaluations,
        IReadOnlySet<string> existingPaths
    );
}
