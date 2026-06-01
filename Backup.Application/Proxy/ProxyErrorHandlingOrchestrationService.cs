using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyErrorHandlingOrchestrationService(
    IProxyRuntimeStatusTransitionService proxyRuntimeStatusTransitionService,
    IProxyErrorTrackingService proxyErrorTrackingService
) : IProxyErrorHandlingOrchestrationService
{
    private readonly IProxyRuntimeStatusTransitionService _proxyRuntimeStatusTransitionService =
        proxyRuntimeStatusTransitionService;
    private readonly IProxyErrorTrackingService _proxyErrorTrackingService = proxyErrorTrackingService;

    public ProxyErrorHandlingOutcome Handle(
        ProxyRuntimeRecord runtimeRecord,
        bool isCurrentlyActive,
        string shortMessage,
        string extendedMessage,
        int errorsToInactiveThreshold,
        DateTime now
    )
    {
        if (!_proxyRuntimeStatusTransitionService.ShouldHandleError(isCurrentlyActive))
            return new ProxyErrorHandlingOutcome { ShouldApplyRuntimeRecord = false };

        ProxyErrorTrackingResult tracking = _proxyErrorTrackingService.RegisterError(
            runtimeRecord,
            shortMessage,
            extendedMessage,
            errorsToInactiveThreshold,
            now
        );
        DateTime? disabledAt = _proxyRuntimeStatusTransitionService.ResolveDisabledAt(
            tracking.WasDisabled,
            now
        );

        return new ProxyErrorHandlingOutcome
        {
            ShouldApplyRuntimeRecord = true,
            WasDisabled = tracking.WasDisabled,
            DisabledAt = disabledAt,
        };
    }
}
