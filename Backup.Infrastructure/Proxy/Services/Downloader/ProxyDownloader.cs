using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Services.Downloader;

public class ProxyDownloader(
    ILogger _logger,
    IProxyEndpointParserService proxyEndpointParserService,
    IProxyProviderTypeResolverService proxyProviderTypeResolverService,
    string format
)
{
    private readonly ILogger _logger = _logger;
    private readonly IProxyEndpointParserService _proxyEndpointParserService =
        proxyEndpointParserService;
    private readonly IProxyProviderTypeResolverService _proxyProviderTypeResolverService =
        proxyProviderTypeResolverService;
    private readonly string _format = format;

    public IProxyDownloader Create(string type)
    {
        ProxyProviderType providerType = _proxyProviderTypeResolverService.Resolve(type);

        return providerType switch
        {
            ProxyProviderType.Http => new ProxyDownloaderHttp(
                _logger,
                _proxyEndpointParserService,
                _format
            ),
            ProxyProviderType.Config => new ProxyDownloaderInLineConfig(
                _proxyEndpointParserService,
                _format
            ),
            _ => throw new NotSupportedException(
                $"Proxy provider type not supported: {providerType}"
            ),
        };
    }
}
