using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Abstractions.Core;

public interface IProxyClientRotationService
{
    HttpClient CreateClient(IReadOnlyList<ProxyData> runtimePool);
}
