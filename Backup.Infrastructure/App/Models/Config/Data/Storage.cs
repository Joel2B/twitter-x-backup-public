namespace Backup.Infrastructure.Models.Config.Data;

public class Storage
{
    public string? Id { get; set; }
    public bool Default { get; set; } = false;
    public required string Type { get; set; }
    public required bool Enabled { get; set; }
    public required List<int> Partitions { get; set; }
    public required Tasks Tasks { get; set; }
}

