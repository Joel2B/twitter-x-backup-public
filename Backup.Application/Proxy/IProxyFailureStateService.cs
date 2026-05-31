using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyFailureStateService
{
    void Initialize(int proxyCount, int proxyIndex = 0);
    ProxyFailureOutcome RegisterFailure(ProxyFailureSettings settings);
    void ResetFailureCount();
    void ResetStopCount();
    ProxyFailureState GetState();
}
