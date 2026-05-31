namespace Backup.Application.Bulk.Models;

public sealed class BulkPrunePartitionExecutionInput
{
    public required int PartitionId { get; init; }
    public required IReadOnlyList<DatedPath> DatedPaths { get; init; }
}
