using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public interface IProxyCheckExecutionService
{
    Task<ProxyHealthAcceptanceResult> ExecuteAsync(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded,
        ISet<string> existingKeys,
        IProxyHealthProbePort probePort,
        int flushEvery = 10,
        CancellationToken cancellationToken = default
    );
}
