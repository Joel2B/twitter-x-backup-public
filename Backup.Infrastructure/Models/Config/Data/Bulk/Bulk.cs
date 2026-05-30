using Backup.Infrastructure.Models.Config.Downloads;

namespace Backup.Infrastructure.Models.Config.Data.Bulk;

public class StorageBulk : Storage
{
    public required Paths Paths { get; set; }
}

public class Paths : PathConfig
{
    public required PathConfig Bulk { get; set; }
    public required PathConfig Sources { get; set; }
}
