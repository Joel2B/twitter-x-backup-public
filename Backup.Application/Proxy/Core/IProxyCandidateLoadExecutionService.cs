using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public interface IProxyCandidateLoadExecutionService
{
    Task<IReadOnlyList<ProxyCandidate>> ExecuteAsync(
        IReadOnlyList<ProxyLoadProviderDefinition> providers,
        IProxyResourceLoadPort port
    );
}
