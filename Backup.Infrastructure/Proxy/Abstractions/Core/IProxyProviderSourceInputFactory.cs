using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Models.Config.Proxy;

namespace Backup.Infrastructure.Proxy.Abstractions.Core;

public interface IProxyProviderSourceInputFactory
{
    IReadOnlyList<ProxyProviderSourceInput> Build(IReadOnlyList<Provider> providers);
}
