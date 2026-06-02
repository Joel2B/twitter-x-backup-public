using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Proxy.Adapters;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Backup.Infrastructure.Proxy.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Services;

public class ProxyException(string? message = null) : Exception(message) { }

public class ProxyEmptyException(string? message = null) : ProxyException(message) { }

// Facade for proxy runtime lifecycle (setup, selection, failure handling, and persistence).
// Keep this public behavior stable while internal collaborators evolve.
public class ProxyProvider(
    ILogger<ProxyProvider> _logger,
    AppConfig _config,
    ProxyProviderDependencies dependencies
) : IProxyProvider, ISetup, IDisposable
{
    private readonly ILogger<ProxyProvider> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IProxyData _data = dependencies.Data;
    private readonly IProxyResourceLoadPort _proxyResourceLoadPort =
        dependencies.ProxyResourceLoadPort;
    private readonly IProxyProviderSourceInputFactory _proxyProviderSourceInputFactory =
        dependencies.ProxyProviderSourceInputFactory;
    private readonly IProxyProviderCandidateLoadOrchestrationService _proxyProviderCandidateLoadOrchestrationService =
        dependencies.ProxyProviderCandidateLoadOrchestrationService;
    private readonly IProxyFailureStateService _proxyFailureStateService =
        dependencies.ProxyFailureStateService;
    private readonly IProxyFailureExecutionPlanService _proxyFailureExecutionPlanService =
        dependencies.ProxyFailureExecutionPlanService;
    private readonly IProxyFailureSettingsPolicyService _proxyFailureSettingsPolicyService =
        dependencies.ProxyFailureSettingsPolicyService;
    private readonly IProxyRuntimeMutationService _proxyRuntimeMutationService =
        dependencies.ProxyRuntimeMutationService;
    private readonly IProxyClientRotationService _proxyClientRotationService =
        dependencies.ProxyClientRotationService;
    private readonly IProxyProviderLifecycleService _proxyProviderLifecycleService =
        dependencies.ProxyProviderLifecycleService;

    private readonly SemaphoreSlim _proxyLock = new(1);

    private List<ProxyData> _proxies = [];

    private volatile HttpClient? _client;

    public async Task Setup()
    {
        await _proxyProviderLifecycleService.CheckAsync(_proxies, LoadCandidatesFromProviders);

        if (!_config.Proxy.Enabled)
        {
            RotateClient();

            return;
        }

        _proxies = await _proxyProviderLifecycleService.SetupRuntimePoolAsync(
            LoadCandidatesFromProviders
        );
        RotateClient();
    }

    public HttpClient GetClient()
    {
        if (_client is null)
            throw new InvalidOperationException();

        return _client;
    }

    public async Task Next(CancellationToken token)
    {
        if (!_config.Proxy.Enabled)
            return;

        try
        {
            await _proxyLock.WaitAsync(token);

            ProxyFailureOutcome outcome = _proxyFailureStateService.RegisterFailure(
                BuildFailureSettings()
            );
            ProxyFailureExecutionPlan plan = _proxyFailureExecutionPlanService.BuildPlan(outcome);

            _logger.LogInformation("failure count: {value}", outcome.State.FailureCount);

            if (plan.ShouldLogAttempt)
                _logger.LogInformation("attempt {attempt}", outcome.State.AttemptCount);

            switch (plan.Action)
            {
                case ProxyFailureExecutionAction.None:
                    return;
                case ProxyFailureExecutionAction.ThrowPoolExhausted:
                    throw new ProxyEmptyException();
                case ProxyFailureExecutionAction.ThrowStopProcess:
                    throw new ProxyException();
                case ProxyFailureExecutionAction.RotateProxy:
                    RotateClient();
                    return;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported proxy failure execution action: {plan.Action}"
                    );
            }
        }
        finally
        {
            _proxyLock.Release();
        }
    }

    public Task Reset()
    {
        if (!_config.Proxy.Enabled)
            return Task.CompletedTask;

        _proxyFailureStateService.ResetFailureCount();

        return Task.CompletedTask;
    }

    private void RotateClient()
    {
        HttpClient client = _proxyClientRotationService.CreateClient(_proxies);
        HttpClient? oldClient = Interlocked.Exchange(ref _client, client);
        oldClient?.Dispose();
    }

    public void OnUse()
    {
        if (!_config.Proxy.Enabled)
            return;

        int proxyIndex = _proxyFailureStateService.GetState().ProxyIndex;
        ProxyData proxy = _proxies[proxyIndex];
        ProxyUseHandlingOutcome outcome;

        lock (proxy)
        {
            outcome = _proxyRuntimeMutationService.HandleUse(
                proxy,
                _proxyFailureStateService.GetState().StopCount
            );
        }

        if (outcome.ShouldLogResetStopCount)
            _logger.LogInformation("count to stop reset");

        _proxyFailureStateService.ResetStopCount();
    }

    public void OnError(Exception ex)
    {
        if (!_config.Proxy.Enabled)
            return;

        int proxyIndex = _proxyFailureStateService.GetState().ProxyIndex;
        ProxyData proxy = _proxies[proxyIndex];

        lock (proxy)
        {
            ProxyErrorHandlingOutcome outcome = _proxyRuntimeMutationService.HandleError(
                proxy,
                ex,
                _config.Proxy.Threshold.ErrorsToInactive
            );

            if (!outcome.ShouldApplyRuntimeRecord)
                return;

            if (outcome.WasDisabled)
            {
                _logger.LogInformation("proxy {proxy} disabled", proxy.Proxy.ToString());
            }
        }
    }

    public async Task SaveData()
    {
        await _data.Save(_proxies);
    }

    private async Task<IReadOnlyList<ProxyCandidate>> LoadCandidatesFromProviders()
    {
        IReadOnlyList<ProxyProviderSourceInput> sources = _proxyProviderSourceInputFactory.Build(
            _config.Proxy.Providers
        );

        return await _proxyProviderCandidateLoadOrchestrationService.ExecuteAsync(
            sources,
            _proxyResourceLoadPort
        );
    }

    private ProxyFailureSettings BuildFailureSettings() =>
        _proxyFailureSettingsPolicyService.Create(
            _config.Downloads.Threads.Start,
            _config.Proxy.Threshold.ErrorsToStop
        );

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
