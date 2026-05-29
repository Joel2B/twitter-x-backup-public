using Backup.Infrastructure.Models.Config.Downloads;

namespace Backup.Infrastructure.Models.Config;

public class DebugConfig : PathConfig
{
    public required List<int> Partitions { get; set; }
    public required PathConfig Log { get; set; }
    public required DebugApi Api { get; set; }
}

public class DebugApi : PathConfig
{
    public required DebugPrune Prune { get; set; }
}

public class DebugPrune
{
    public bool Enabled { get; set; }
    public int RetainedCountLimit { get; set; }
}

