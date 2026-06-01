using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxySetupExecutionService(
    IProxyProviderRuntimeOrchestrationService proxyProviderRuntimeOrchestrationService,
    IProxySetupOrchestrationService proxySetupOrchestrationService
) : IProxySetupExecutionService
{
    private readonly IProxyProviderRuntimeOrchestrationService _proxyProviderRuntimeOrchestrationService =
        proxyProviderRuntimeOrchestrationService;
    private readonly IProxySetupOrchestrationService _proxySetupOrchestrationService =
        proxySetupOrchestrationService;

    public ProxySetupExecutionResult Execute(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded
    )
    {
        IReadOnlyList<ProxyRuntimeRecord> runtimePool =
            _proxyProviderRuntimeOrchestrationService.BuildRuntimePool(stored, loaded);

        ProxySetupPlan setupPlan = _proxySetupOrchestrationService.BuildPlan(runtimePool.Count);

        return new ProxySetupExecutionResult { RuntimePool = runtimePool, SetupPlan = setupPlan };
    }
}
