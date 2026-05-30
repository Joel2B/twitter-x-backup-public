using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Downloads;
using Backup.Infrastructure.Models.Config.Medias;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Models.Config.Tasks;

namespace Backup.Infrastructure.Models.Config;

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
