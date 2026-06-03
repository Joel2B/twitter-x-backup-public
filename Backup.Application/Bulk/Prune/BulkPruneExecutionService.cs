using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public sealed class BulkPruneExecutionService(
    IBulkPrunePartitionPlanningService bulkPrunePartitionPlanningService
) : IBulkPruneExecutionService
{
    private readonly IBulkPrunePartitionPlanningService _bulkPrunePartitionPlanningService =
        bulkPrunePartitionPlanningService;

    public IReadOnlyList<BulkPrunePartitionExecutionPlan> PlanPartitions(
        IReadOnlyList<BulkPrunePartitionExecutionInput> partitions,
        bool pruneEnabled,
        int keepDays
    )
    {
        if (!pruneEnabled || partitions.Count == 0)
            return [];

        List<BulkPrunePartitionExecutionPlan> plans = [];

        foreach (BulkPrunePartitionExecutionInput partition in partitions)
        {
            if (partition.DatedPaths.Count == 0)
                continue;

            BulkPrunePartitionPlan plan = _bulkPrunePartitionPlanningService.Plan(
                partition.DatedPaths,
                keepDays
            );

            plans.Add(
                new BulkPrunePartitionExecutionPlan
                {
                    PartitionId = partition.PartitionId,
                    ThresholdDate = plan.ThresholdDate,
                    PathsToRemove = plan.PathsToRemove,
                }
            );
        }

        return plans;
    }
}
