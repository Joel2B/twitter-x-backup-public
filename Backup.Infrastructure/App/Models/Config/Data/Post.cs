using Backup.App.Models.Config.Downloads;

namespace Backup.App.Models.Config.Data.Posts;

public class StoragePost : Storage
{
    public required Paths Paths { get; set; }
}

public class Paths : PathConfig
{
    public required PathConfig Post { get; set; }
}
