namespace Backup.Application.Partition.Models;

public sealed class PartitionPathProbeTarget
{
    public required string PartitionName { get; init; }
    public required string ProbePath { get; init; }
}
