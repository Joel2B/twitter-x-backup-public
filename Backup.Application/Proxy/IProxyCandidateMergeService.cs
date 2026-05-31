using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyCandidateMergeService
{
    IReadOnlyList<ProxyCandidate> MergeDistinct(
        IEnumerable<ProxyCandidate> primary,
        IEnumerable<ProxyCandidate> secondary
    );
}
