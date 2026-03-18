namespace Backup.App.Models.Config.Data.Media;

public class Storage : Models.Config.Data.Storage
{
    public required Paths Paths { get; set; }
    public long SizeHeavy { get; set; }
}

public class Paths : Downloads.Path
{
    public required Downloads.Path Media { get; set; }
    public required Downloads.Path Cache { get; set; }
    public required Tmp Tmp { get; set; }
}
