namespace Backup.App.Models.Config;

public class Debug : Downloads.Path
{
    public required List<int> Partitions { get; set; }
    public required Downloads.Path Log { get; set; }
    public required DebugApi Api { get; set; }
}

public class DebugApi : Downloads.Path
{
    public required DebugPrune Prune { get; set; }
}

public class DebugPrune
{
    public bool Enabled { get; set; }
    public int RetainedCountLimit { get; set; }
}
