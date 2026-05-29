using Backup.App.Models.Config.Proxy;
using Backup.App.Models.Proxy;

namespace Backup.App.Interfaces.Proxy;

public interface IProxyDownloader
{
    public Task<List<ProxyDataConfig>?> Load(Resource resource);
}
