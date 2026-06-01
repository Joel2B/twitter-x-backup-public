using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public sealed class ProxyCandidateLoadService(
    IProxySourceLoadService proxySourceLoadService
) : IProxyCandidateLoadService
{
    private readonly IProxySourceLoadService _proxySourceLoadService = proxySourceLoadService;

    public async Task<IReadOnlyList<ProxyCandidate>> Load(
        IReadOnlyList<ProxyLoadProviderDefinition> providers,
        IProxyResourceLoadPort port
    )
    {
        IReadOnlyList<ProxyEndpoint> loaded = await _proxySourceLoadService.Load(providers, port);

        return loaded
            .Select(endpoint => new ProxyCandidate
            {
                Ip = endpoint.Ip,
                Port = endpoint.Port,
                Protocol = endpoint.Protocol,
            })
            .DistinctBy(
                candidate => $"{candidate.Protocol}|{candidate.Ip}|{candidate.Port}",
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();
    }
}
