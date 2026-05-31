using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyErrorTrackingService
{
    ProxyErrorTrackingResult RegisterError(
        ProxyRuntimeRecord proxy,
        string shortMessage,
        string extendedMessage,
        int errorsToInactiveThreshold,
        DateTime now
    );
}
