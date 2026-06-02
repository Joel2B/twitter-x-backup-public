using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Abstractions.Core;

public interface IProxyProviderLifecycleService
{
    Task CheckAsync(
        List<ProxyData> runtimePool,
        Func<Task<IReadOnlyList<ProxyCandidate>>> loadCandidates
    );

    Task<List<ProxyData>> SetupRuntimePoolAsync(
        Func<Task<IReadOnlyList<ProxyCandidate>>> loadCandidates
    );
}
