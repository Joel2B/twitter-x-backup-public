namespace Backup.App.Models.Media;

public class Cache
{
    public required string Path { get; set; }
    public Size? Size { get; set; }
    public int? PartitionId { get; set; }
}

public class Size
{
    public long? Stream { get; set; }
    public long? File { get; set; }
}
