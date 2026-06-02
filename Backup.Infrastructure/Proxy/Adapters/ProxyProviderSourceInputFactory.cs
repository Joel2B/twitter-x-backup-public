using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Models.Config.Proxy;

namespace Backup.Infrastructure.Proxy.Adapters;

public sealed class ProxyProviderSourceInputFactory
{
    public IReadOnlyList<ProxyProviderSourceInput> Build(IReadOnlyList<Provider> providers) =>
        providers
            .Select(provider => new ProxyProviderSourceInput
            {
                ProviderType = provider.Type,
                ProviderFormat = provider.Format,
                Resources = provider
                    .Resources.Select(resource => new ProxyProviderSourceResourceInput
                    {
                        ResourceType = resource.Type,
                        ResourceValue = resource.Value,
                    })
                    .ToList(),
            })
            .ToList();
}
