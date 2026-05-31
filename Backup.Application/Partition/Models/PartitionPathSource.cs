namespace Backup.Application.Partition.Models;

public sealed class PartitionPathSource
{
    public required IReadOnlyList<string> Paths { get; init; }
    public required IReadOnlyDictionary<string, string> Aliases { get; init; }
    public required string BaseDirectory { get; init; }
}
