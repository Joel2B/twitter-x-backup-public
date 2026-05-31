using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyRuntimePoolProjectionService(
    IProxyRuntimePoolSelectionService runtimePoolSelectionService,
    IProxyKeyPolicyService keyPolicyService
) : IProxyRuntimePoolProjectionService
{
    private readonly IProxyRuntimePoolSelectionService _runtimePoolSelectionService =
        runtimePoolSelectionService;
    private readonly IProxyKeyPolicyService _keyPolicyService = keyPolicyService;

    public IReadOnlyList<ProxyRuntimeRecord> SelectPool(IEnumerable<ProxyRuntimeRecord> proxies)
    {
        List<ProxyRuntimeRecord> source = proxies.ToList();

        IReadOnlySet<string> selectedKeys = _runtimePoolSelectionService.SelectKeys(
            source.Select(proxy => new ProxyRuntimePoolCandidate
            {
                Key = BuildKey(proxy.Candidate),
                IsActive = proxy.IsActive,
                ConnectionCount = proxy.Connections.Count,
            })
        );

        return source.Where(proxy => selectedKeys.Contains(BuildKey(proxy.Candidate))).ToList();
    }

    private string BuildKey(ProxyCandidate candidate) =>
        _keyPolicyService.Build(candidate.Ip, candidate.Port, candidate.Protocol);
}
