using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Models.Proxy;

namespace Backup.Infrastructure.Interfaces.Proxy;

public interface IProxyDownloader
{
    public Task<List<ProxyDataConfig>?> Load(Resource resource);
}
