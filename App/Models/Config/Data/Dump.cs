namespace Backup.App.Models.Config.Data.Dump;

public class Storage : Models.Config.Data.Storage
{
    public required Paths Paths { get; set; }
}

public class Paths : Downloads.Path
{
    public required Dumps Dumps { get; set; }
}

public class Dumps : Downloads.Path
{
    public required Dump Dump { get; set; }
}

public class Dump : Downloads.Path
{
    public required Downloads.Path Api { get; set; }
}
