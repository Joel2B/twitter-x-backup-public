using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyCandidateMergeService : IProxyCandidateMergeService
{
    public IReadOnlyList<ProxyCandidate> MergeDistinct(
        IEnumerable<ProxyCandidate> primary,
        IEnumerable<ProxyCandidate> secondary
    )
    {
        HashSet<string> added = [];
        List<ProxyCandidate> result = [];

        Append(primary, added, result);
        Append(secondary, added, result);

        return result;
    }

    private static void Append(
        IEnumerable<ProxyCandidate> source,
        HashSet<string> added,
        List<ProxyCandidate> result
    )
    {
        foreach (ProxyCandidate candidate in source)
        {
            string key = BuildKey(candidate);

            if (!added.Add(key))
                continue;

            result.Add(candidate);
        }
    }

    private static string BuildKey(ProxyCandidate candidate) =>
        $"{candidate.Ip}|{candidate.Port}|{candidate.Protocol}";
}
