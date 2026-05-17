using Backup.App.Models.Config.Downloads;

namespace Backup.App.Models.Config.Data.Backup;

public class StorageBackup : Storage
{
    public required Paths Paths { get; set; }
    public required ChunkConfig Chunk { get; set; }
    public required Direct Direct { get; set; }
}

public class Paths : PathConfig
{
    public required PathConfig Cache { get; set; }
}
