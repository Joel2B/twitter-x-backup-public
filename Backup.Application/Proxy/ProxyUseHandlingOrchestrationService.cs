using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyUseHandlingOrchestrationService(
    IProxyUsageTrackingService proxyUsageTrackingService
) : IProxyUseHandlingOrchestrationService
{
    private readonly IProxyUsageTrackingService _proxyUsageTrackingService = proxyUsageTrackingService;

    public ProxyUseHandlingOutcome HandleUse(
        ProxyRuntimeRecord runtimeRecord,
        DateTime now,
        int stopCount
    )
    {
        _proxyUsageTrackingService.RegisterUse(runtimeRecord, now);

        return new ProxyUseHandlingOutcome
        {
            ShouldLogResetStopCount = stopCount > 0,
        };
    }
}
