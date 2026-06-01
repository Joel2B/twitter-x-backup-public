using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyRuntimePoolSelectionService(
    IProxyRuntimePolicyService runtimePolicyService
) : IProxyRuntimePoolSelectionService
{
    private readonly IProxyRuntimePolicyService _runtimePolicyService = runtimePolicyService;

    public IReadOnlySet<string> SelectKeys(IEnumerable<ProxyRuntimePoolCandidate> candidates) =>
        candidates
            .Where(candidate =>
                _runtimePolicyService.ShouldIncludeInRuntimePool(
                    candidate.IsActive,
                    candidate.ConnectionCount
                )
            )
            .Select(candidate => candidate.Key)
            .ToHashSet();
}
