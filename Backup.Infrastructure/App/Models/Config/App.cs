using Backup.App.Models.Config.Data;
using Backup.App.Models.Config.Downloads;
using Backup.App.Models.Config.Medias;
using Backup.App.Models.Config.Proxy;
using Backup.App.Models.Config.Tasks;

namespace Backup.App.Models.Config;

public class AppConfig
{
    public required List<Api.UsersContext> UsersContext { get; set; }
    public required Dictionary<string, FetchItem> Fetch { get; set; }
    public required ServicesConfig Services { get; set; }
    public required DataConfig Data { get; set; }
    public required DownloadsConfig Downloads { get; set; }
    public required MediasConfig Medias { get; set; }
    public required ProxyConfig Proxy { get; set; }
    public required DebugConfig Debug { get; set; }
    public required TasksConfig Tasks { get; set; }
    public required BulkConfig Bulk { get; set; }
    public required NetworkConfig Network { get; set; }
}
