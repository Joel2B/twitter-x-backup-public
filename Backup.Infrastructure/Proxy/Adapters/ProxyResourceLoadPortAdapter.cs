using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Services.Downloader;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Adapters;

public sealed class ProxyResourceLoadPortAdapter(
    ILogger logger,
    IProxyEndpointParserService proxyEndpointParserService,
    IProxyProviderTypeResolverService proxyProviderTypeResolverService
) : IProxyResourceLoadPort
{
    private readonly ILogger _logger = logger;
    private readonly IProxyEndpointParserService _proxyEndpointParserService =
        proxyEndpointParserService;
    private readonly IProxyProviderTypeResolverService _proxyProviderTypeResolverService =
        proxyProviderTypeResolverService;

    public async Task<IReadOnlyList<ProxyEndpoint>> Load(ProxyLoadRequest request)
    {
        IProxyDownloader downloader = new ProxyDownloader(
            _logger,
            _proxyEndpointParserService,
            _proxyProviderTypeResolverService,
            request.ProviderFormat
        ).Create(request.ProviderType);

        Resource resource = new() { Type = request.ResourceType, Value = request.ResourceValue };
        List<Backup.Infrastructure.Proxy.Models.ProxyDataConfig>? loaded = await downloader.Load(
            resource
        );

        if (loaded is null || loaded.Count == 0)
            return [];

        return loaded
            .Select(proxy => new ProxyEndpoint
            {
                Ip = proxy.Ip,
                Port = proxy.Port,
                Protocol = proxy.Protocol,
            })
            .ToList();
    }
}
