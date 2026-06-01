using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyFailureSettingsPolicyService
{
    ProxyFailureSettings Create(int downloadThreadStart, int errorsToStop);
}
