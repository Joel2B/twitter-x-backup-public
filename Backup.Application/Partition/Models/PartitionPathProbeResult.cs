namespace Backup.Application.Partition.Models;

public sealed class PartitionPathProbeResult
{
    public required string PartitionName { get; init; }
    public string? Error { get; init; }
}
