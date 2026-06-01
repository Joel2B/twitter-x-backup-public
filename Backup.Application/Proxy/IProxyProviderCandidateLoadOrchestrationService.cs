using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public interface IProxyProviderCandidateLoadOrchestrationService
{
    Task<IReadOnlyList<ProxyCandidate>> ExecuteAsync(
        IReadOnlyList<ProxyProviderSourceInput> sources,
        IProxyResourceLoadPort port
    );
}
