using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public interface IProxyHealthProbeService
{
    Task<ProxyHealthProbeResult> Probe(
        ProxyCandidate candidate,
        IProxyHealthProbePort port,
        CancellationToken cancellationToken = default
    );
}
