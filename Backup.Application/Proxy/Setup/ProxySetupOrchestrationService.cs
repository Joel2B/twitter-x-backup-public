using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxySetupOrchestrationService : IProxySetupOrchestrationService
{
    public ProxySetupPlan BuildPlan(int runtimePoolCount) =>
        new()
        {
            ShouldThrowPoolEmpty = runtimePoolCount == 0,
            ShouldInitializeFailureState = runtimePoolCount > 0,
            ShouldPersistPool = runtimePoolCount > 0,
        };
}
