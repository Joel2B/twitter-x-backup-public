using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyProviderTypeResolverService : IProxyProviderTypeResolverService
{
    public ProxyProviderType Resolve(string type) =>
        type.ToLowerInvariant() switch
        {
            "http" => ProxyProviderType.Http,
            "config" => ProxyProviderType.Config,
            _ => throw new NotSupportedException($"Proxy provider type not supported: {type}"),
        };
}
