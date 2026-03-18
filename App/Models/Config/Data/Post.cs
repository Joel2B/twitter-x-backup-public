namespace Backup.App.Models.Config.Data.Post;

public class Storage : Models.Config.Data.Storage
{
    public required Paths Paths { get; set; }
}

public class Paths : Downloads.Path
{
    public required Downloads.Path Post { get; set; }
}
