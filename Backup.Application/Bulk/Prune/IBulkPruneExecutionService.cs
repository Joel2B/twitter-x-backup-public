using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public interface IBulkPruneExecutionService
{
    IReadOnlyList<BulkPrunePartitionExecutionPlan> PlanPartitions(
        IReadOnlyList<BulkPrunePartitionExecutionInput> partitions,
        bool pruneEnabled,
        int keepDays
    );
}
