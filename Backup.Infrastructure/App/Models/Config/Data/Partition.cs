using Backup.App.Models.Config.Downloads;

namespace Backup.App.Models.Config.Data;

public class PartitionConfig : PathConfig
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public required string Type { get; set; }
    public List<string>? Tags { get; set; }
    public required int Size { get; set; }
    public required int UsableSpace { get; set; }
    public bool Enabled { get; set; } = true;
}
