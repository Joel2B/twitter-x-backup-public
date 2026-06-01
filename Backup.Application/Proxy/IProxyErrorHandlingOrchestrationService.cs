using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyErrorHandlingOrchestrationService
{
    ProxyErrorHandlingOutcome Handle(
        ProxyRuntimeRecord runtimeRecord,
        bool isCurrentlyActive,
        string shortMessage,
        string extendedMessage,
        int errorsToInactiveThreshold,
        DateTime now
    );
}
