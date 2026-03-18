namespace Backup.App.Models.Config.Data;

public class Tmp : Downloads.Path
{
    public required Downloads.Path Downloader { get; set; }
    public required Downloads.Path Downloaded { get; set; }
}
