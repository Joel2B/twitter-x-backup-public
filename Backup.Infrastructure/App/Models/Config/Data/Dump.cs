using Backup.Infrastructure.Models.Config.Downloads;

namespace Backup.Infrastructure.Models.Config.Data.Dump;

public class StorageDump : Storage
{
    public required Paths Paths { get; set; }
}

public class Paths : PathConfig
{
    public required Dumps Dumps { get; set; }
}

public class Dumps : PathConfig
{
    public required Dump Dump { get; set; }
}

public class Dump : PathConfig
{
    public required PathConfig Api { get; set; }
}

