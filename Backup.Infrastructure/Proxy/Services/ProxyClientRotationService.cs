using Backup.Application.Proxy;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Services;

public sealed class ProxyClientRotationService(
    AppConfig config,
    IProxyFailureStateService proxyFailureStateService,
    IProxyHttpClientFactoryPolicyService proxyHttpClientFactoryPolicyService,
    IProxyHttpClientHeaderPolicyService proxyHttpClientHeaderPolicyService,
    ILogger<ProxyClientRotationService> logger
) : IProxyClientRotationService
{
    private readonly AppConfig _config = config;
    private readonly IProxyFailureStateService _proxyFailureStateService = proxyFailureStateService;
    private readonly IProxyHttpClientFactoryPolicyService _proxyHttpClientFactoryPolicyService =
        proxyHttpClientFactoryPolicyService;
    private readonly IProxyHttpClientHeaderPolicyService _proxyHttpClientHeaderPolicyService =
        proxyHttpClientHeaderPolicyService;
    private readonly ILogger<ProxyClientRotationService> _logger = logger;

    public HttpClient CreateClient(IReadOnlyList<ProxyData> runtimePool)
    {
        Uri? proxyUri = null;

        if (_config.Proxy.Enabled)
        {
            int proxyIndex = _proxyFailureStateService.GetState().ProxyIndex;
            string proxy = runtimePool[proxyIndex].Proxy.ToString();
            proxyUri = new Uri(proxy);
            _logger.LogInformation("Uri: {uri}", proxyUri.ToString());
        }

        HttpClientHandler handler = _proxyHttpClientFactoryPolicyService.CreateHandler(proxyUri);
        HttpClient client = _proxyHttpClientFactoryPolicyService.CreateClient(
            handler,
            TimeSpan.FromSeconds(_config.Downloads.Timeout)
        );

        _proxyHttpClientHeaderPolicyService.Apply(client.DefaultRequestHeaders);

        return client;
    }
}
