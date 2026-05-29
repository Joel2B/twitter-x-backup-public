using Backup.Infrastructure.Models.Config.Downloads;

namespace Backup.Infrastructure.Models.Config.Data.Media;

public class StorageMedia : Storage
{
    public required Paths Paths { get; set; }
    public long SizeHeavy { get; set; }
}

public class Paths : PathConfig
{
    public required PathConfig Media { get; set; }
    public required PathConfig Cache { get; set; }
    public required Tmp Tmp { get; set; }
}

