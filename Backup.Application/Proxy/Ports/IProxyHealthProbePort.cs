using System.Net;
using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy.Ports;

public interface IProxyHealthProbePort
{
    Task<HttpStatusCode> Send(
        ProxyCandidate candidate,
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken
    );
}
