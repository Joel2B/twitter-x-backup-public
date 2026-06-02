using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Services;

public class ProxyException(string? message = null) : Exception(message) { }

public class ProxyEmptyException(string? message = null) : ProxyException(message) { }

// Facade for proxy runtime lifecycle (setup, selection, failure handling, and persistence).
// Keep this public behavior stable while internal collaborators evolve.
public class ProxyProvider(
    ProxyProviderDependencies dependencies
) : IProxyProvider, ISetup, IDisposable
{
    private readonly IProxyData _data = dependencies.Data;
    private readonly IProxyProviderLifecycleService _proxyProviderLifecycleService =
        dependencies.ProxyProviderLifecycleService;
    private readonly ProxyProviderCandidateLoader _candidateLoader = new(
        dependencies.Config,
        dependencies.ProxyResourceLoadPort,
        dependencies.ProxyProviderSourceInputFactory,
        dependencies.ProxyProviderCandidateLoadOrchestrationService
    );
    private readonly ProxyProviderClientSession _clientSession = new(
        dependencies.ProxyClientRotationService
    );
    private readonly ProxyProviderRuntimeEventCoordinator _runtimeEvents = new(
        dependencies.Logger,
        dependencies.Config,
        dependencies.ProxyFailureStateService,
        dependencies.ProxyFailureExecutionPlanService,
        dependencies.ProxyFailureSettingsPolicyService,
        dependencies.ProxyRuntimeMutationService
    );

    private readonly SemaphoreSlim _proxyLock = new(1);

    private List<ProxyData> _proxies = [];

    public async Task Setup()
    {
        await _proxyProviderLifecycleService.CheckAsync(_proxies, _candidateLoader.LoadAsync);

        if (!dependencies.Config.Proxy.Enabled)
        {
            RotateClient();

            return;
        }

        _proxies = await _proxyProviderLifecycleService.SetupRuntimePoolAsync(_candidateLoader.LoadAsync);
        RotateClient();
    }

    public HttpClient GetClient() => _clientSession.GetClient();

    public async Task Next(CancellationToken token)
    {
        if (!dependencies.Config.Proxy.Enabled)
            return;

        try
        {
            await _proxyLock.WaitAsync(token);
            _runtimeEvents.ExecuteNext(RotateClient);
        }
        finally
        {
            _proxyLock.Release();
        }
    }

    public Task Reset()
    {
        _runtimeEvents.Reset();
        return Task.CompletedTask;
    }

    private void RotateClient() => _clientSession.Rotate(_proxies);

    public void OnUse() => _runtimeEvents.OnUse(_proxies);

    public void OnError(Exception ex) => _runtimeEvents.OnError(_proxies, ex);

    public async Task SaveData()
    {
        await _data.Save(_proxies);
    }

    public void Dispose()
    {
        _clientSession.Dispose();
    }
}
