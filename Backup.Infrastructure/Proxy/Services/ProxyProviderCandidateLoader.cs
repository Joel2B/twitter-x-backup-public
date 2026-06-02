using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Proxy.Abstractions.Core;

namespace Backup.Infrastructure.Proxy.Services;

internal sealed class ProxyProviderCandidateLoader(
    AppConfig config,
    IProxyResourceLoadPort proxyResourceLoadPort,
    IProxyProviderSourceInputFactory proxyProviderSourceInputFactory,
    IProxyProviderCandidateLoadOrchestrationService proxyProviderCandidateLoadOrchestrationService
)
{
    private readonly AppConfig _config = config;
    private readonly IProxyResourceLoadPort _proxyResourceLoadPort = proxyResourceLoadPort;
    private readonly IProxyProviderSourceInputFactory _proxyProviderSourceInputFactory =
        proxyProviderSourceInputFactory;
    private readonly IProxyProviderCandidateLoadOrchestrationService _proxyProviderCandidateLoadOrchestrationService =
        proxyProviderCandidateLoadOrchestrationService;

    public Task<IReadOnlyList<ProxyCandidate>> LoadAsync()
    {
        IReadOnlyList<ProxyProviderSourceInput> sources = _proxyProviderSourceInputFactory.Build(
            _config.Proxy.Providers
        );

        return _proxyProviderCandidateLoadOrchestrationService.ExecuteAsync(
            sources,
            _proxyResourceLoadPort
        );
    }
}
