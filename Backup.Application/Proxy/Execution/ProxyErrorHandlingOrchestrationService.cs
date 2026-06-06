using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyErrorHandlingOrchestrationService(
    IProxyErrorTrackingService proxyErrorTrackingService
) : IProxyErrorHandlingOrchestrationService
{
    private readonly IProxyErrorTrackingService _proxyErrorTrackingService =
        proxyErrorTrackingService;

    public ProxyErrorHandlingOutcome Handle(
        ProxyRuntimeRecord runtimeRecord,
        bool isCurrentlyActive,
        string shortMessage,
        string extendedMessage,
        int errorsToInactiveThreshold,
        DateTime now
    )
    {
        if (!isCurrentlyActive)
            return new ProxyErrorHandlingOutcome { ShouldApplyRuntimeRecord = false };

        ProxyErrorTrackingResult tracking = _proxyErrorTrackingService.RegisterError(
            runtimeRecord,
            shortMessage,
            extendedMessage,
            errorsToInactiveThreshold,
            now
        );
        DateTime? disabledAt = tracking.WasDisabled ? now : null;

        return new ProxyErrorHandlingOutcome
        {
            ShouldApplyRuntimeRecord = true,
            WasDisabled = tracking.WasDisabled,
            DisabledAt = disabledAt,
        };
    }
}
