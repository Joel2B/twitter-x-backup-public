using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public interface IProxyHealthAcceptanceService
{
    Task<ProxyHealthAcceptanceResult> AcceptAsync(
        IEnumerable<ProxyRuntimeRecord> merged,
        ISet<string> existingKeys,
        int flushEvery,
        IProxyHealthProbePort probePort,
        CancellationToken cancellationToken = default
    );
}
