using Backup.App.Models.Config.Downloads;

namespace Backup.App.Models.Config.Data;

public class Tmp : PathConfig
{
    public required PathConfig Downloader { get; set; }
    public required PathConfig Downloaded { get; set; }
}
