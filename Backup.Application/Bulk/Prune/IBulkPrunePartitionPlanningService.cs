using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public interface IBulkPrunePartitionPlanningService
{
    BulkPrunePartitionPlan Plan(IReadOnlyList<DatedPath> datedPaths, int keepDays);
}
