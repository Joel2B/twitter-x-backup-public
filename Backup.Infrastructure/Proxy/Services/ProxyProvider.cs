using System.Net;
using Backup.Application.Core;
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
    private readonly IProxyHealthProbePort _proxyHealthProbePort =
        dependencies.ProxyHealthProbePort;
    private readonly IProxyResourceLoadPort _proxyResourceLoadPort =
        dependencies.ProxyResourceLoadPort;
    private readonly IProxyHttpClientFactoryPolicyService _proxyHttpClientFactoryPolicyService =
        dependencies.ProxyHttpClientFactoryPolicyService;
    private readonly IProxyHttpClientHeaderPolicyService _proxyHttpClientHeaderPolicyService =
        dependencies.ProxyHttpClientHeaderPolicyService;
    private readonly IProxyKeyPolicyService _proxyKeyPolicyService =
        dependencies.ProxyKeyPolicyService;
    private readonly IProxyProviderCandidateLoadOrchestrationService _proxyProviderCandidateLoadOrchestrationService =
        dependencies.ProxyProviderCandidateLoadOrchestrationService;
    private readonly IProxySetupExecutionService _proxySetupExecutionService =
        dependencies.ProxySetupExecutionService;
    private readonly IProxyCheckExecutionService _proxyCheckExecutionService =
        dependencies.ProxyCheckExecutionService;
    private readonly ProxyRuntimeRecordMapper _proxyRuntimeRecordMapper =
        dependencies.ProxyRuntimeRecordMapper;
    private readonly IProxyAcceptanceApplyOrchestrationService _proxyAcceptanceApplyOrchestrationService =
        dependencies.ProxyAcceptanceApplyOrchestrationService;
    private readonly IProxyFailureStateService _proxyFailureStateService =
        dependencies.ProxyFailureStateService;
    private readonly IProxyFailureExecutionPlanService _proxyFailureExecutionPlanService =
        dependencies.ProxyFailureExecutionPlanService;
    private readonly IProxyFailureSettingsPolicyService _proxyFailureSettingsPolicyService =
        dependencies.ProxyFailureSettingsPolicyService;
    private readonly IProxyUseHandlingOrchestrationService _proxyUseHandlingOrchestrationService =
        dependencies.ProxyUseHandlingOrchestrationService;
    private readonly IProxyErrorHandlingOrchestrationService _proxyErrorHandlingOrchestrationService =
        dependencies.ProxyErrorHandlingOrchestrationService;
    private readonly IDateTimeProvider _dateTimeProvider = dependencies.DateTimeProvider;

    private readonly SemaphoreSlim _proxyLock = new(1);

    private List<ProxyData> _proxies = [];

    private volatile HttpClient? _client;

    public async Task Setup()
    {
        await Check();

        if (!_config.Proxy.Enabled)
        {
            NewClient();

            return;
        }

        List<ProxyData> stored = (await _data.GetAllAsDictionary() ?? []).Values.ToList();
        ProxySetupExecutionResult setup = _proxySetupExecutionService.Execute(
            stored.Select(_proxyRuntimeRecordMapper.ToRuntimeRecord),
            await LoadCandidatesFromProviders()
        );
        _proxies = setup.RuntimePool.Select(_proxyRuntimeRecordMapper.ToProxyData).ToList();

        if (setup.SetupPlan.ShouldThrowPoolEmpty)
            throw new ProxyEmptyException();

        if (setup.SetupPlan.ShouldInitializeFailureState)
            _proxyFailureStateService.Initialize(_proxies.Count);

        if (setup.SetupPlan.ShouldPersistPool)
            await SaveData();

        NewClient();
    }

    private async Task Check()
    {
        if (!_config.Proxy.Check)
            return;

        HashSet<string> proxiesAdded = [.. _proxies.Select(o => GetProxyKey(o.Proxy))];
        List<ProxyData> proxiesStorage = await _data.GetAll() ?? [];
        ProxyHealthAcceptanceResult acceptance = await _proxyCheckExecutionService.ExecuteAsync(
            proxiesStorage.Select(_proxyRuntimeRecordMapper.ToRuntimeRecord),
            await LoadCandidatesFromProviders(),
            proxiesAdded,
            _proxyHealthProbePort,
            flushEvery: 10
        );

        foreach (string error in acceptance.ProbeErrors)
            _logger.LogError("Error: {error}", error);

        await _proxyAcceptanceApplyOrchestrationService.ApplyAsync(
            acceptance.AcceptedItems,
            record =>
            {
                _proxies.Add(_proxyRuntimeRecordMapper.ToProxyData(record));
                return Task.CompletedTask;
            },
            SaveData
        );
    }

    private string GetProxyKey(ProxyDataConfig proxy) =>
        _proxyKeyPolicyService.Build(proxy.Ip, proxy.Port, proxy.Protocol);

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
                    NewClient();
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

    private void NewClient()
    {
        Uri? proxyUri = null;

        if (_config.Proxy.Enabled)
        {
            int proxyIndex = _proxyFailureStateService.GetState().ProxyIndex;
            string proxy = _proxies[proxyIndex].Proxy.ToString();
            proxyUri = new Uri(proxy);

            _logger.LogInformation("Uri: {uri}", proxyUri.ToString());
        }

        HttpClientHandler handler = _proxyHttpClientFactoryPolicyService.CreateHandler(proxyUri);
        HttpClient client = _proxyHttpClientFactoryPolicyService.CreateClient(
            handler,
            TimeSpan.FromSeconds(_config.Downloads.Timeout)
        );

        _proxyHttpClientHeaderPolicyService.Apply(client.DefaultRequestHeaders);

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
            ProxyRuntimeRecord runtimeRecord = _proxyRuntimeRecordMapper.ToRuntimeRecord(proxy);
            outcome = _proxyUseHandlingOrchestrationService.HandleUse(
                runtimeRecord,
                _dateTimeProvider.Now,
                _proxyFailureStateService.GetState().StopCount
            );
            _proxyRuntimeRecordMapper.ApplyRuntimeRecord(proxy, runtimeRecord, disabledAt: null);
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
            DateTime now = _dateTimeProvider.Now;
            ProxyRuntimeRecord runtimeRecord = _proxyRuntimeRecordMapper.ToRuntimeRecord(proxy);
            ProxyErrorHandlingOutcome outcome = _proxyErrorHandlingOrchestrationService.Handle(
                runtimeRecord,
                proxy.Status.Current == StatusEnum.Active,
                ex.Message,
                ex.ToString(),
                _config.Proxy.Threshold.ErrorsToInactive,
                now
            );

            if (!outcome.ShouldApplyRuntimeRecord)
                return;

            _proxyRuntimeRecordMapper.ApplyRuntimeRecord(proxy, runtimeRecord, outcome.DisabledAt);

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
        IReadOnlyList<ProxyProviderSourceInput> sources = _config
            .Proxy.Providers.Select(ToProviderDefinition)
            .ToList();

        return await _proxyProviderCandidateLoadOrchestrationService.ExecuteAsync(
            sources,
            _proxyResourceLoadPort
        );
    }

    private static ProxyProviderSourceInput ToProviderDefinition(Provider provider) =>
        new()
        {
            ProviderType = provider.Type,
            ProviderFormat = provider.Format,
            Resources = provider
                .Resources.Select(resource => new ProxyProviderSourceResourceInput
                {
                    ResourceType = resource.Type,
                    ResourceValue = resource.Value,
                })
                .ToList(),
        };

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
