using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxySetupExecutionService
{
    ProxySetupExecutionResult Execute(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded
    );
}
