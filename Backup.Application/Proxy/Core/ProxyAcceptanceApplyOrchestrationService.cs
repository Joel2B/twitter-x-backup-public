using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyAcceptanceApplyOrchestrationService
    : IProxyAcceptanceApplyOrchestrationService
{
    public async Task ApplyAsync(
        IReadOnlyList<ProxyHealthAcceptanceItem> acceptedItems,
        Func<ProxyRuntimeRecord, Task> addRecord,
        Func<Task> flush
    )
    {
        foreach (ProxyHealthAcceptanceItem item in acceptedItems)
        {
            await addRecord(item.Record);

            if (item.ShouldFlush)
                await flush();
        }

        await flush();
    }
}
