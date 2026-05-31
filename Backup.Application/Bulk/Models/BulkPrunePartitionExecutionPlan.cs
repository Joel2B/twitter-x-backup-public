namespace Backup.Application.Bulk.Models;

public sealed class BulkPrunePartitionExecutionPlan
{
    public required int PartitionId { get; init; }
    public DateTime? ThresholdDate { get; init; }
    public required IReadOnlyCollection<string> PathsToRemove { get; init; }
}
