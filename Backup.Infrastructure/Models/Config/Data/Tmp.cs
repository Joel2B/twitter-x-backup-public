using Backup.Infrastructure.Models.Config.Downloads;

namespace Backup.Infrastructure.Models.Config.Data;

public class Tmp : PathConfig
{
    public required PathConfig Downloader { get; set; }
    public required PathConfig Downloaded { get; set; }
}
