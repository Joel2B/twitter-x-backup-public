namespace Backup.App.Models.Config;

public class Debug : Downloads.Path
{
    public required List<int> Partitions { get; set; }
    public required Downloads.Path Log { get; set; }
    public required DebugApi Api { get; set; }
    public required MediaDebug Media { get; set; }
}

public class DebugApi : Downloads.Path
{
    public required DebugPrune Prune { get; set; }
}

public class MediaDebug : Downloads.Path
{
    public required Downloads.Path Log { get; set; }
    public required MediaDebugUrl Url { get; set; }
    public required Downloads.Path Error { get; set; }
}

public class MediaDebugUrl : Downloads.Path
{
    public required DebugPrune Prune { get; set; }
}

public class DebugPrune
{
    public bool Enabled { get; set; }
    public int RetainedCountLimit { get; set; }
}
