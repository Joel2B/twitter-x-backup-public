using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyUsageTrackingService
{
    void RegisterUse(ProxyRuntimeRecord proxy, DateTime now);
}
