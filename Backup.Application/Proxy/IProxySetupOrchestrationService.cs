using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxySetupOrchestrationService
{
    ProxySetupPlan BuildPlan(int runtimePoolCount);
}
