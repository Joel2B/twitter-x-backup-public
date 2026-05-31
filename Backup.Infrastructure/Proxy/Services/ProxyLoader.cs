using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Proxy.Adapters;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Proxy.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Services;

public class ProxyLoader(
    ILogger _logger,
    AppConfig _config,
    IProxyEndpointParserService proxyEndpointParserService,
    IProxyProviderTypeResolverService proxyProviderTypeResolverService,
    IProxySourceLoadService proxySourceLoadService
)
{
    private readonly ILogger _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IProxyEndpointParserService _proxyEndpointParserService = proxyEndpointParserService;
    private readonly IProxyProviderTypeResolverService _proxyProviderTypeResolverService =
        proxyProviderTypeResolverService;
    private readonly IProxySourceLoadService _proxySourceLoadService = proxySourceLoadService;

    public async Task<List<ProxyDataConfig>> Load()
    {
        IReadOnlyList<ProxyLoadProviderDefinition> providers = _config.Proxy.Providers
            .Select(ToProviderDefinition)
            .ToList();

        IProxyResourceLoadPort port = new ProxyResourceLoadPortAdapter(
            _logger,
            _proxyEndpointParserService,
            _proxyProviderTypeResolverService
        );

        IReadOnlyList<ProxyEndpoint> loaded = await _proxySourceLoadService.Load(providers, port);

        return loaded
            .Select(endpoint => new ProxyDataConfig
            {
                Ip = endpoint.Ip,
                Port = endpoint.Port,
                Protocol = endpoint.Protocol,
            })
            .ToList();
    }

    private static ProxyLoadProviderDefinition ToProviderDefinition(Provider provider) =>
        new()
        {
            ProviderType = provider.Type,
            ProviderFormat = provider.Format,
            Resources = provider.Resources
                .Select(resource => new ProxyLoadResourceDefinition
                {
                    ResourceType = resource.Type,
                    ResourceValue = resource.Value,
                })
                .ToList(),
        };
}
