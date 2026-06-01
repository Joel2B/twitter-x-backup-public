using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public sealed class ProxyCandidateLoadExecutionService(
    IProxyCandidateLoadService proxyCandidateLoadService
) : IProxyCandidateLoadExecutionService
{
    private readonly IProxyCandidateLoadService _proxyCandidateLoadService = proxyCandidateLoadService;

    public Task<IReadOnlyList<ProxyCandidate>> ExecuteAsync(
        IReadOnlyList<ProxyLoadProviderDefinition> providers,
        IProxyResourceLoadPort port
    ) => _proxyCandidateLoadService.Load(providers, port);
}
