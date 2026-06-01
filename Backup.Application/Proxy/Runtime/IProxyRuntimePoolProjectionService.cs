using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyRuntimePoolProjectionService
{
    IReadOnlyList<ProxyRuntimeRecord> SelectPool(IEnumerable<ProxyRuntimeRecord> proxies);
}
