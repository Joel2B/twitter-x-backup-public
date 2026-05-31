using System.Net;
using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public sealed class ProxyHealthProbeService(IProxyHealthCheckPolicyService healthCheckPolicyService)
    : IProxyHealthProbeService
{
    private readonly IProxyHealthCheckPolicyService _healthCheckPolicyService = healthCheckPolicyService;

    public async Task<ProxyHealthProbeResult> Probe(
        ProxyCandidate candidate,
        IProxyHealthProbePort port,
        CancellationToken cancellationToken = default
    )
    {
        ProxyCandidate current = candidate;
        string url = _healthCheckPolicyService.GetHealthCheckUrl();
        TimeSpan timeout = _healthCheckPolicyService.GetHealthCheckTimeout();

        while (true)
        {
            try
            {
                HttpStatusCode statusCode = await port.Send(
                    current,
                    url,
                    timeout,
                    cancellationToken
                );

                return new ProxyHealthProbeResult
                {
                    Candidate = current,
                    StatusCode = statusCode,
                    Success = statusCode == HttpStatusCode.OK,
                };
            }
            catch (Exception ex)
            {
                bool canFallback =
                    _healthCheckPolicyService.ShouldFallbackToHttp(ex)
                    && !string.Equals(current.Protocol, "http", StringComparison.OrdinalIgnoreCase);

                if (canFallback)
                {
                    current = new ProxyCandidate
                    {
                        Ip = current.Ip,
                        Port = current.Port,
                        Protocol = "http",
                    };
                    continue;
                }

                return new ProxyHealthProbeResult
                {
                    Candidate = current,
                    StatusCode = null,
                    Success = false,
                    Error = ex,
                };
            }
        }
    }
}
