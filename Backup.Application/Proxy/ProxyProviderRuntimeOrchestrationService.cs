using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public sealed class ProxyProviderRuntimeOrchestrationService(
    IProxyRuntimePoolBuilderService proxyRuntimePoolBuilderService,
    IProxyHealthAcceptanceService proxyHealthAcceptanceService
) : IProxyProviderRuntimeOrchestrationService
{
    private readonly IProxyRuntimePoolBuilderService _proxyRuntimePoolBuilderService =
        proxyRuntimePoolBuilderService;
    private readonly IProxyHealthAcceptanceService _proxyHealthAcceptanceService =
        proxyHealthAcceptanceService;

    public IReadOnlyList<ProxyRuntimeRecord> BuildRuntimePool(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded
    ) => _proxyRuntimePoolBuilderService.BuildPool(stored, loaded);

    public Task<ProxyHealthAcceptanceResult> AcceptCandidatesAsync(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded,
        ISet<string> existingKeys,
        int flushEvery,
        IProxyHealthProbePort probePort,
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyList<ProxyRuntimeRecord> merged = _proxyRuntimePoolBuilderService.BuildPool(
            stored,
            loaded
        );

        return _proxyHealthAcceptanceService.AcceptAsync(
            merged,
            existingKeys,
            flushEvery,
            probePort,
            cancellationToken
        );
    }
}
