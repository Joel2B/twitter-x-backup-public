using Backup.Infrastructure.Models.Config.Downloads;

namespace Backup.Infrastructure.Models.Config.Data.Media;

public class StorageMedia : Storage
{
    public required Paths Paths { get; set; }
    public long SizeHeavy { get; set; }
    public List<MediaCacheConfig> Cache { get; set; } = [];
}

public class Paths : PathConfig
{
    public required PathConfig Media { get; set; }
    public required Tmp Tmp { get; set; }
}

public class MediaCacheConfig
{
    public string? Id { get; set; }
    public bool Default { get; set; } = false;
    public bool Enabled { get; set; } = true;
    public string Type { get; set; } = "json";
    public string? ConnectionString { get; set; }
    public PathConfig? Path { get; set; }
    public List<int> Partitions { get; set; } = [];
}
