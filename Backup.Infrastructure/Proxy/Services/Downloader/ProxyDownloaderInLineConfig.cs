using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Services.Downloader;

public class ProxyDownloaderInLineConfig(
    IProxyEndpointParserService proxyEndpointParserService,
    string format
) : IProxyDownloader
{
    private readonly IProxyEndpointParserService _proxyEndpointParserService =
        proxyEndpointParserService;
    private readonly string _format = format;

    public Task<List<ProxyDataConfig>?> Load(Resource resource)
    {
        List<string> lines = [resource.Value];
        IReadOnlyList<ProxyEndpoint> endpoints = _proxyEndpointParserService.Parse(
            _format,
            lines,
            resource.Type
        );

        List<ProxyDataConfig> proxies = endpoints
            .Select(endpoint => new ProxyDataConfig
            {
                Ip = endpoint.Ip,
                Port = endpoint.Port,
                Protocol = endpoint.Protocol,
            })
            .ToList();

        return Task.FromResult<List<ProxyDataConfig>?>(proxies);
    }
}
