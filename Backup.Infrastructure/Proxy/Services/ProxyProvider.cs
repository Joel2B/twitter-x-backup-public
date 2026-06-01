using System.Net;
using Backup.Application.Proxy;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Backup.Infrastructure.Proxy.Adapters;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Proxy.Models;
using Backup.Application.Proxy.Models;
using Backup.Application.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Proxy.Services;

public class ProxyException(string? message = null) : Exception(message) { }

public class ProxyEmptyException(string? message = null) : ProxyException(message) { }

public class ProxyProvider(
    ILogger<ProxyProvider> _logger,
    AppConfig _config,
    IProxyData _data,
    IProxyHealthProbePort proxyHealthProbePort,
    IProxyHttpClientFactoryPolicyService proxyHttpClientFactoryPolicyService,
    IProxyHttpClientHeaderPolicyService proxyHttpClientHeaderPolicyService,
    IProxyKeyPolicyService proxyKeyPolicyService,
    IProxyEndpointParserService proxyEndpointParserService,
    IProxyProviderTypeResolverService proxyProviderTypeResolverService,
    IProxyCandidateLoadService proxyCandidateLoadService,
    IProxyRuntimeRecordMapper proxyRuntimeRecordMapper,
    IProxyProviderRuntimeOrchestrationService proxyProviderRuntimeOrchestrationService,
    IProxyAcceptanceApplyOrchestrationService proxyAcceptanceApplyOrchestrationService,
    IProxyFailureStateService proxyFailureStateService,
    IProxyFailureExecutionPlanService proxyFailureExecutionPlanService,
    IProxyFailureSettingsPolicyService proxyFailureSettingsPolicyService,
    IProxyUseHandlingOrchestrationService proxyUseHandlingOrchestrationService,
    IProxyErrorHandlingOrchestrationService proxyErrorHandlingOrchestrationService,
    IDateTimeProvider dateTimeProvider
)
    : IProxyProvider,
        ISetup,
        IDisposable
{
    private readonly ILogger<ProxyProvider> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IProxyData _data = _data;
    private readonly IProxyHealthProbePort _proxyHealthProbePort = proxyHealthProbePort;
    private readonly IProxyHttpClientFactoryPolicyService _proxyHttpClientFactoryPolicyService =
        proxyHttpClientFactoryPolicyService;
    private readonly IProxyHttpClientHeaderPolicyService _proxyHttpClientHeaderPolicyService =
        proxyHttpClientHeaderPolicyService;
    private readonly IProxyKeyPolicyService _proxyKeyPolicyService = proxyKeyPolicyService;
    private readonly IProxyEndpointParserService _proxyEndpointParserService =
        proxyEndpointParserService;
    private readonly IProxyProviderTypeResolverService _proxyProviderTypeResolverService =
        proxyProviderTypeResolverService;
    private readonly IProxyCandidateLoadService _proxyCandidateLoadService = proxyCandidateLoadService;
    private readonly IProxyRuntimeRecordMapper _proxyRuntimeRecordMapper = proxyRuntimeRecordMapper;
    private readonly IProxyProviderRuntimeOrchestrationService _proxyProviderRuntimeOrchestrationService =
        proxyProviderRuntimeOrchestrationService;
    private readonly IProxyAcceptanceApplyOrchestrationService _proxyAcceptanceApplyOrchestrationService =
        proxyAcceptanceApplyOrchestrationService;
    private readonly IProxyFailureStateService _proxyFailureStateService = proxyFailureStateService;
    private readonly IProxyFailureExecutionPlanService _proxyFailureExecutionPlanService =
        proxyFailureExecutionPlanService;
    private readonly IProxyFailureSettingsPolicyService _proxyFailureSettingsPolicyService =
        proxyFailureSettingsPolicyService;
    private readonly IProxyUseHandlingOrchestrationService _proxyUseHandlingOrchestrationService =
        proxyUseHandlingOrchestrationService;
    private readonly IProxyErrorHandlingOrchestrationService _proxyErrorHandlingOrchestrationService =
        proxyErrorHandlingOrchestrationService;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

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

        List<ProxyData> stored = (await _data.GetAllAsDictionary() ?? [])
            .Values
            .ToList();
        IReadOnlyList<ProxyRuntimeRecord> runtimePool =
            _proxyProviderRuntimeOrchestrationService.BuildRuntimePool(
                stored.Select(_proxyRuntimeRecordMapper.ToRuntimeRecord),
                await LoadCandidatesFromProviders()
            );
        _proxies = runtimePool.Select(_proxyRuntimeRecordMapper.ToProxyData).ToList();

        if (_proxies.Count == 0)
            throw new ProxyEmptyException();

        _proxyFailureStateService.Initialize(_proxies.Count);
        await SaveData();
        NewClient();
    }

    private async Task Check()
    {
        if (!_config.Proxy.Check)
            return;

        HashSet<string> proxiesAdded = [.. _proxies.Select(o => GetProxyKey(o.Proxy))];
        List<ProxyData> proxiesStorage = await _data.GetAll() ?? [];
        ProxyHealthAcceptanceResult acceptance =
            await _proxyProviderRuntimeOrchestrationService.AcceptCandidatesAsync(
                proxiesStorage.Select(_proxyRuntimeRecordMapper.ToRuntimeRecord),
                await LoadCandidatesFromProviders(),
                proxiesAdded,
                flushEvery: 10,
                _proxyHealthProbePort
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
                JsonConvert.SerializeObject(ex),
                _config.Proxy.Threshold.ErrorsToInactive,
                now
            );

            if (!outcome.ShouldApplyRuntimeRecord)
                return;

            _proxyRuntimeRecordMapper.ApplyRuntimeRecord(
                proxy,
                runtimeRecord,
                outcome.DisabledAt
            );

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
        IReadOnlyList<ProxyLoadProviderDefinition> providers = _config.Proxy.Providers
            .Select(ToProviderDefinition)
            .ToList();
        IProxyResourceLoadPort port = new ProxyResourceLoadPortAdapter(
            _logger,
            _proxyEndpointParserService,
            _proxyProviderTypeResolverService
        );

        return await _proxyCandidateLoadService.Load(providers, port);
    }

    private static ProxyLoadProviderDefinition ToProviderDefinition(Provider provider) =>
        new()
        {
            ProviderType = provider.Type,
            ProviderFormat = provider.Format,
            Resources = provider.Resources
                .Select(resource => new ProxyLoadResourceDefinition
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
