namespace Backup.App.Models.Config.Data.Backup;

public class Storage : Models.Config.Data.Storage
{
    public required Paths Paths { get; set; }
    public required Chunk Chunk { get; set; }
    public required Direct Direct { get; set; }
}

public class Paths : Downloads.Path
{
    public required Downloads.Path Cache { get; set; }
}
