using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyAcceptanceApplyOrchestrationService
{
    Task ApplyAsync(
        IReadOnlyList<ProxyHealthAcceptanceItem> acceptedItems,
        Func<ProxyRuntimeRecord, Task> addRecord,
        Func<Task> flush
    );
}
