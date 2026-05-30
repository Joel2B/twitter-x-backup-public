using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Models.Proxy;

namespace Backup.Infrastructure.Proxy.Abstractions.Core;

public interface IProxyDownloader
{
    public Task<List<ProxyDataConfig>?> Load(Resource resource);
}
