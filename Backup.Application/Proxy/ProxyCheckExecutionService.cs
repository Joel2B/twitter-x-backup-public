using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public sealed class ProxyCheckExecutionService(
    IProxyProviderRuntimeOrchestrationService proxyProviderRuntimeOrchestrationService
) : IProxyCheckExecutionService
{
    private readonly IProxyProviderRuntimeOrchestrationService _proxyProviderRuntimeOrchestrationService =
        proxyProviderRuntimeOrchestrationService;

    public Task<ProxyHealthAcceptanceResult> ExecuteAsync(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded,
        ISet<string> existingKeys,
        IProxyHealthProbePort probePort,
        int flushEvery = 10,
        CancellationToken cancellationToken = default
    ) =>
        _proxyProviderRuntimeOrchestrationService.AcceptCandidatesAsync(
            stored,
            loaded,
            existingKeys,
            flushEvery,
            probePort,
            cancellationToken
        );
}
