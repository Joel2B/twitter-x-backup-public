namespace Backup.Application.Partition.Models;

public sealed class PartitionPathProbeExecutionResult
{
    public required IReadOnlyList<PartitionPathProbeResult> Results { get; init; }
    public bool HasErrors { get; init; }
}
