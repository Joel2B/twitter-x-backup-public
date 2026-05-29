using Backup.App.Interfaces.Proxy;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Proxy.Downloader;

public class ProxyDownloader(ILogger _logger, IProxyFormatter _formatter)
{
    private readonly ILogger _logger = _logger;
    private readonly IProxyFormatter _formatter = _formatter;

    public IProxyDownloader Create(string type)
    {
        return type.ToLower() switch
        {
            "http" => new ProxyDownloaderHttp(_logger, _formatter),
            "config" => new ProxyDownloaderInLineConfig(_formatter),
            _ => throw new NotSupportedException($"Tipo no soportado: {type}"),
        };
    }
}
