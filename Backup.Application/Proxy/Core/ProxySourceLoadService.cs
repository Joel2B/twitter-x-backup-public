using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public sealed class ProxySourceLoadService : IProxySourceLoadService
{
    public async Task<IReadOnlyList<ProxyEndpoint>> Load(
        IReadOnlyList<ProxyLoadProviderDefinition> providers,
        IProxyResourceLoadPort port
    )
    {
        List<ProxyEndpoint> proxies = [];

        foreach (ProxyLoadProviderDefinition provider in providers)
        {
            foreach (ProxyLoadResourceDefinition resource in provider.Resources)
            {
                ProxyLoadRequest request = new()
                {
                    ProviderType = provider.ProviderType,
                    ProviderFormat = provider.ProviderFormat,
                    ResourceType = resource.ResourceType,
                    ResourceValue = resource.ResourceValue,
                };

                IReadOnlyList<ProxyEndpoint> loaded = await port.Load(request);

                if (loaded.Count > 0)
                    proxies.AddRange(loaded);
            }
        }

        return proxies;
    }
}
