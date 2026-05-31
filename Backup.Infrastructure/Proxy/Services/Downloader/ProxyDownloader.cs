using Backup.Application.Proxy;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Services.Downloader;

public class ProxyDownloader(
    ILogger _logger,
    IProxyEndpointParserService proxyEndpointParserService,
    string format
)
{
    private readonly ILogger _logger = _logger;
    private readonly IProxyEndpointParserService _proxyEndpointParserService = proxyEndpointParserService;
    private readonly string _format = format;

    public IProxyDownloader Create(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "http" => new ProxyDownloaderHttp(_logger, _proxyEndpointParserService, _format),
            "config" => new ProxyDownloaderInLineConfig(_proxyEndpointParserService, _format),
            _ => throw new NotSupportedException($"Tipo no soportado: {type}"),
        };
    }
}
