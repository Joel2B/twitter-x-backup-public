using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyFailureOrchestrationService
{
    ProxyFailureOutcome EvaluateFailure(ProxyFailureState state, ProxyFailureSettings settings);
    ProxyFailureState ResetFailureCount(ProxyFailureState state);
    ProxyFailureState ResetStopCount(ProxyFailureState state);
}
