using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyUseHandlingOrchestrationService
{
    ProxyUseHandlingOutcome HandleUse(ProxyRuntimeRecord runtimeRecord, DateTime now, int stopCount);
}
