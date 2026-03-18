namespace Backup.App.Models.Config.Data.Bulk;

public class Storage : Models.Config.Data.Storage
{
    public required Paths Paths { get; set; }
}

public class Paths : Downloads.Path
{
    public required Downloads.Path Bulk { get; set; }
    public required Downloads.Path Sources { get; set; }
}
