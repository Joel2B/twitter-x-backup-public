namespace Backup.App.Models.Config.Data;

public class Storage
{
    public string? Id { get; set; }
    public required string Type { get; set; }
    public required bool Enabled { get; set; }
    public required List<int> Partitions { get; set; }
    public required Tasks Tasks { get; set; }
}
