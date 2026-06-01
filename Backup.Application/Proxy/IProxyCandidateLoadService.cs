using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public interface IProxyCandidateLoadService
{
    Task<IReadOnlyList<ProxyCandidate>> Load(
        IReadOnlyList<ProxyLoadProviderDefinition> providers,
        IProxyResourceLoadPort port
    );
}
