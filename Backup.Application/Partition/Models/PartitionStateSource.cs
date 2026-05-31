namespace Backup.Application.Partition.Models;

public sealed class PartitionStateSource
{
    public required int Id { get; init; }
    public required string Type { get; init; }
    public required List<string>? Tags { get; init; }
    public required int Size { get; init; }
    public required int UsableSpace { get; init; }
    public required bool Enabled { get; init; }
    public required long CurrentSize { get; init; }
}
