using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public interface IProxyProviderRuntimeOrchestrationService
{
    IReadOnlyList<ProxyRuntimeRecord> BuildRuntimePool(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded
    );
    Task<ProxyHealthAcceptanceResult> AcceptCandidatesAsync(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded,
        ISet<string> existingKeys,
        int flushEvery,
        IProxyHealthProbePort probePort,
        CancellationToken cancellationToken = default
    );
}
