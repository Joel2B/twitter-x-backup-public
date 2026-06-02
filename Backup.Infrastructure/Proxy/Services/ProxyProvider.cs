using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Services;

public class ProxyException(string? message = null) : Exception(message) { }

public class ProxyEmptyException(string? message = null) : ProxyException(message) { }

// Facade for proxy runtime lifecycle (setup, selection, failure handling, and persistence).
// Keep this public behavior stable while internal collaborators evolve.
public class ProxyProvider : IProxyProvider, ISetup, IDisposable
{
    private readonly AppConfig _config;
    private readonly IProxyData _data;
    private readonly ProxyProviderLifecycleService _proxyProviderLifecycleService;
    private readonly ProxyProviderCandidateLoader _candidateLoader;
    private readonly ProxyProviderClientSession _clientSession;
    private readonly ProxyProviderRuntimeEventCoordinator _runtimeEvents;

    private readonly SemaphoreSlim _proxyLock = new(1);

    private List<ProxyData> _proxies = [];

    internal ProxyProvider(
        AppConfig config,
        IProxyData data,
        ProxyProviderLifecycleService proxyProviderLifecycleService,
        ProxyProviderCandidateLoader candidateLoader,
        ProxyProviderClientSession clientSession,
        ProxyProviderRuntimeEventCoordinator runtimeEvents
    )
    {
        _config = config;
        _data = data;
        _proxyProviderLifecycleService = proxyProviderLifecycleService;
        _candidateLoader = candidateLoader;
        _clientSession = clientSession;
        _runtimeEvents = runtimeEvents;
    }

    public async Task Setup()
    {
        await _proxyProviderLifecycleService.CheckAsync(_proxies, _candidateLoader.LoadAsync);

        if (!_config.Proxy.Enabled)
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
        if (!_config.Proxy.Enabled)
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
