using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyRuntimePoolBuilderService
{
    IReadOnlyList<ProxyRuntimeRecord> BuildPool(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded
    );
}
