using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyRuntimeRecordMergeService(
    IProxyCandidateMergeService candidateMergeService,
    IProxyKeyPolicyService keyPolicyService
) : IProxyRuntimeRecordMergeService
{
    private readonly IProxyCandidateMergeService _candidateMergeService = candidateMergeService;
    private readonly IProxyKeyPolicyService _keyPolicyService = keyPolicyService;

    public IReadOnlyList<ProxyRuntimeRecord> MergeStoredAndLoaded(
        IEnumerable<ProxyRuntimeRecord> stored,
        IEnumerable<ProxyCandidate> loaded
    )
    {
        Dictionary<string, ProxyRuntimeRecord> storedByKey = stored.ToDictionary(
            item => BuildKey(item.Candidate),
            item => item
        );

        IReadOnlyList<ProxyCandidate> mergedCandidates = _candidateMergeService.MergeDistinct(
            stored.Select(item => item.Candidate),
            loaded
        );

        List<ProxyRuntimeRecord> result = [];
        foreach (ProxyCandidate candidate in mergedCandidates)
        {
            string key = BuildKey(candidate);
            if (storedByKey.TryGetValue(key, out ProxyRuntimeRecord? record))
            {
                result.Add(record);
                continue;
            }

            result.Add(new ProxyRuntimeRecord { Candidate = candidate });
        }

        return result;
    }

    private string BuildKey(ProxyCandidate candidate) =>
        _keyPolicyService.Build(candidate.Ip, candidate.Port, candidate.Protocol);
}
