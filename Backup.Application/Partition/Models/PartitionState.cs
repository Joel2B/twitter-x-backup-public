namespace Backup.Application.Partition.Models;

public sealed class PartitionState
{
    public int Id { get; set; }
    public required string Type { get; set; }
    public List<string>? Tags { get; set; }
    public int Size { get; set; }
    public int UsableSpace { get; set; }
    public bool Enabled { get; set; }
    public long CurrentSize { get; set; }
}
