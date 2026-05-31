namespace Backup.Application.Bulk.Models;

public sealed class BulkPrunePartitionPlan
{
    public DateTime? ThresholdDate { get; init; }
    public required IReadOnlyList<string> PathsToRemove { get; init; }
}
