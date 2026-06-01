using System.Net;
using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Adapters;

public sealed class ProxyHealthProbePortAdapter(
    ILogger<ProxyHealthProbePortAdapter> logger,
    IProxyHttpClientFactoryPolicyService proxyHttpClientFactoryPolicyService
) : IProxyHealthProbePort
{
    private readonly ILogger _logger = logger;
    private readonly IProxyHttpClientFactoryPolicyService _proxyHttpClientFactoryPolicyService =
        proxyHttpClientFactoryPolicyService;

    public async Task<HttpStatusCode> Send(
        ProxyCandidate candidate,
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        Uri proxyUri = new($"{candidate.Protocol}://{candidate.Ip}:{candidate.Port}");
        _logger.LogInformation("Uri: {uri}", proxyUri.ToString());

        using HttpClientHandler handler = _proxyHttpClientFactoryPolicyService.CreateHandler(
            proxyUri
        );
        using HttpClient client = _proxyHttpClientFactoryPolicyService.CreateClient(
            handler,
            timeout
        );
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        return response.StatusCode;
    }
}
