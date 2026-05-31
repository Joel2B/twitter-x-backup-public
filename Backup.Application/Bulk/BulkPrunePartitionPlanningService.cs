using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public sealed class BulkPrunePartitionPlanningService(
    IBulkPrunePolicyService bulkPrunePolicyService
) : IBulkPrunePartitionPlanningService
{
    private readonly IBulkPrunePolicyService _bulkPrunePolicyService = bulkPrunePolicyService;

    public BulkPrunePartitionPlan Plan(IReadOnlyList<DatedPath> datedPaths, int keepDays)
    {
        if (datedPaths.Count == 0)
            return new BulkPrunePartitionPlan { ThresholdDate = null, PathsToRemove = [] };

        DateTime thresholdDate = datedPaths.Max(path => path.Date).AddDays(-Math.Max(0, keepDays));
        IReadOnlyList<string> pathsToRemove = _bulkPrunePolicyService.GetPathsToRemove(
            datedPaths,
            keepDays
        );

        return new BulkPrunePartitionPlan
        {
            ThresholdDate = thresholdDate,
            PathsToRemove = pathsToRemove,
        };
    }
}
