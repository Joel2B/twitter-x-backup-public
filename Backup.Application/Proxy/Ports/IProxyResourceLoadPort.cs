using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy.Ports;

public interface IProxyResourceLoadPort
{
    Task<IReadOnlyList<ProxyEndpoint>> Load(ProxyLoadRequest request);
}
