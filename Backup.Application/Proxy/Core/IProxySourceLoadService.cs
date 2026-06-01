using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public interface IProxySourceLoadService
{
    Task<IReadOnlyList<ProxyEndpoint>> Load(
        IReadOnlyList<ProxyLoadProviderDefinition> providers,
        IProxyResourceLoadPort port
    );
}
