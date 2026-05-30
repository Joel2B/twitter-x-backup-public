using Backup.Infrastructure.Models.Config.Downloads;

namespace Backup.Infrastructure.Models.Config.Data.Posts;

public class StoragePost : Storage
{
    public required Paths Paths { get; set; }
}

public class Paths : PathConfig
{
    public required PathConfig Post { get; set; }
}
