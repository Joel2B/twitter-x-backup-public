using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyProviderTypeResolverService
{
    ProxyProviderType Resolve(string type);
}
