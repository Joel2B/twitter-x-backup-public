using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Application.Proxy;

public sealed class ProxyProviderCandidateLoadOrchestrationService(
    IProxyCandidateLoadExecutionService proxyCandidateLoadExecutionService
) : IProxyProviderCandidateLoadOrchestrationService
{
    private readonly IProxyCandidateLoadExecutionService _proxyCandidateLoadExecutionService =
        proxyCandidateLoadExecutionService;

    public async Task<IReadOnlyList<ProxyCandidate>> ExecuteAsync(
        IReadOnlyList<ProxyProviderSourceInput> sources,
        IProxyResourceLoadPort port
    )
    {
        IReadOnlyList<ProxyLoadProviderDefinition> providers = sources
            .Select(source => new ProxyLoadProviderDefinition
            {
                ProviderType = source.ProviderType,
                ProviderFormat = source.ProviderFormat,
                Resources = source
                    .Resources.Select(resource => new ProxyLoadResourceDefinition
                    {
                        ResourceType = resource.ResourceType,
                        ResourceValue = resource.ResourceValue,
                    })
                    .ToList(),
            })
            .ToList();

        return await _proxyCandidateLoadExecutionService.ExecuteAsync(providers, port);
    }
}
