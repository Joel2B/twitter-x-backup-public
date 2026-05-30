namespace Backup.Infrastructure.Media.Models;

public class MediaCacheEntry
{
    public required string Path { get; set; }
    public MediaCacheSize? Size { get; set; }
    public int? PartitionId { get; set; }
}

public class MediaCacheSize
{
    public long? Stream { get; set; }
    public long? File { get; set; }
}
