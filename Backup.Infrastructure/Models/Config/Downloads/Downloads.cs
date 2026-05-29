namespace Backup.Infrastructure.Models.Config.Downloads;

public class DownloadsConfig
{
    public required bool Enabled { get; set; }
    public required Threads Threads { get; set; }
    public required int Count { get; set; }
    public required long MaxBytesPerSecond { get; set; }
    public long MaxBytes { get; set; }
    public int Timeout { get; set; }
    public int NoDataTimeout { get; set; }
    public required Filter Prune { get; set; }
    public required MediaDebug Media { get; set; }
}

public class Threads
{
    public required int Start { get; set; }
    public required int Min { get; set; }
    public required int Max { get; set; }
}

public class MediaDebug : PathConfig
{
    public required List<int> Partitions { get; set; }
    public required PathConfig Log { get; set; }
    public required PathConfig Error { get; set; }
}

