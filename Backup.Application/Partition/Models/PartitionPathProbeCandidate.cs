namespace Backup.Application.Partition.Models;

public sealed class PartitionPathProbeCandidate
{
    public required string PartitionName { get; init; }
    public required bool Enabled { get; init; }
    public required string RootPath { get; init; }
}
