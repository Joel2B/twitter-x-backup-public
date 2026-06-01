using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyRuntimeRecordMergeService
{
    IReadOnlyList<ProxyRuntimeRecord> MergeStoredAndLoaded(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded
    );
}
