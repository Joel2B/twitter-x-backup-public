using System.Net;
using Backup.Application.Proxy;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Proxy.Models;
using Backup.Application.Proxy.Models;
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
    IProxySourceLoadService proxySourceLoadService,
    IProxyRuntimePoolBuilderService proxyRuntimePoolBuilderService,
    IProxyHealthAcceptanceService proxyHealthAcceptanceService,
    IProxyFailureOrchestrationService proxyFailureOrchestrationService,
    IProxyRuntimeStatusTransitionService proxyRuntimeStatusTransitionService,
    IProxyUsageTrackingService proxyUsageTrackingService,
    IProxyErrorTrackingService proxyErrorTrackingService
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
    private readonly IProxySourceLoadService _proxySourceLoadService = proxySourceLoadService;
    private readonly IProxyRuntimePoolBuilderService _proxyRuntimePoolBuilderService =
        proxyRuntimePoolBuilderService;
    private readonly IProxyHealthAcceptanceService _proxyHealthAcceptanceService =
        proxyHealthAcceptanceService;
    private readonly IProxyFailureOrchestrationService _proxyFailureOrchestrationService =
        proxyFailureOrchestrationService;
    private readonly IProxyRuntimeStatusTransitionService _proxyRuntimeStatusTransitionService =
        proxyRuntimeStatusTransitionService;
    private readonly IProxyUsageTrackingService _proxyUsageTrackingService = proxyUsageTrackingService;
    private readonly IProxyErrorTrackingService _proxyErrorTrackingService = proxyErrorTrackingService;

    private readonly SemaphoreSlim _proxyLock = new(1);
    private int _proxyIndex = 0;
    private int _failureCount = 0;
    private int _stopCount = 0;

    private int count = 0;

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
        IReadOnlyList<ProxyRuntimeRecord> runtimePool = _proxyRuntimePoolBuilderService.BuildPool(
            stored.Select(ToRuntimeRecord),
            await LoadCandidatesFromProviders()
        );
        _proxies = runtimePool.Select(ToProxyData).ToList();

        if (_proxies.Count == 0)
            throw new ProxyEmptyException();

        await SaveData();
        NewClient();
    }

    private async Task Check()
    {
        if (!_config.Proxy.Check)
            return;

        HashSet<string> proxiesAdded = [.. _proxies.Select(o => GetProxyKey(o.Proxy))];
        List<ProxyData> proxiesStorage = await _data.GetAll() ?? [];
        IReadOnlyList<ProxyRuntimeRecord> merged = _proxyRuntimePoolBuilderService.BuildPool(
            proxiesStorage.Select(ToRuntimeRecord),
            await LoadCandidatesFromProviders()
        );
        ProxyHealthAcceptanceResult acceptance = await _proxyHealthAcceptanceService.AcceptAsync(
            merged,
            proxiesAdded,
            flushEvery: 10,
            _proxyHealthProbePort
        );

        foreach (string error in acceptance.ProbeErrors)
            _logger.LogError("Error: {error}", error);

        foreach (ProxyHealthAcceptanceItem item in acceptance.AcceptedItems)
        {
            _proxies.Add(ToProxyData(item.Record));

            if (item.ShouldFlush)
                await SaveData();
        }

        await SaveData();
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

            ProxyFailureOutcome outcome = _proxyFailureOrchestrationService.EvaluateFailure(
                GetFailureState(),
                BuildFailureSettings()
            );
            ApplyFailureState(outcome.State);

            _logger.LogInformation("failure count: {value}", _failureCount);

            if (!outcome.ShouldAttemptSwitch)
                return;

            _logger.LogInformation("attempt {attempt}", count);

            if (!outcome.ShouldRotateProxy)
                return;

            if (outcome.IsPoolExhausted)
                throw new ProxyEmptyException();

            if (outcome.ShouldStopProcess)
                throw new ProxyException();

            NewClient();
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

        ProxyFailureState state = _proxyFailureOrchestrationService.ResetFailureCount(
            GetFailureState()
        );
        _failureCount = state.FailureCount;

        return Task.CompletedTask;
    }

    private void NewClient()
    {
        Uri? proxyUri = null;

        if (_config.Proxy.Enabled)
        {
            string proxy = _proxies[_proxyIndex].Proxy.ToString();
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

        ProxyData proxy = _proxies[_proxyIndex];

        lock (proxy)
        {
            ProxyRuntimeRecord runtimeRecord = ToRuntimeRecord(proxy);
            _proxyUsageTrackingService.RegisterUse(runtimeRecord, DateTime.Now);
            ApplyRuntimeRecord(
                proxy,
                runtimeRecord,
                disabledAt: null,
                _proxyRuntimeStatusTransitionService
            );
        }

        ResetStopCount();
    }

    public void OnError(Exception ex)
    {
        if (!_config.Proxy.Enabled)
            return;

        ProxyData proxy = _proxies[_proxyIndex];

        lock (proxy)
        {
            if (
                !_proxyRuntimeStatusTransitionService.ShouldHandleError(
                    proxy.Status.Current == StatusEnum.Active
                )
            )
                return;

            DateTime now = DateTime.Now;
            ProxyRuntimeRecord runtimeRecord = ToRuntimeRecord(proxy);
            ProxyErrorTrackingResult tracking = _proxyErrorTrackingService.RegisterError(
                runtimeRecord,
                ex.Message,
                JsonConvert.SerializeObject(ex),
                _config.Proxy.Threshold.ErrorsToInactive,
                now
            );
            DateTime? disabledAt = _proxyRuntimeStatusTransitionService.ResolveDisabledAt(
                tracking.WasDisabled,
                now
            );
            ApplyRuntimeRecord(proxy, runtimeRecord, disabledAt, _proxyRuntimeStatusTransitionService);

            if (tracking.WasDisabled)
            {
                _logger.LogInformation("proxy {proxy} disabled", proxy.Proxy.ToString());
            }
        }
    }

    private void ResetStopCount()
    {
        if (_stopCount > 0)
            _logger.LogInformation("count to stop reset");

        ProxyFailureState state = _proxyFailureOrchestrationService.ResetStopCount(GetFailureState());
        _stopCount = state.StopCount;
    }

    public async Task SaveData()
    {
        await _data.Save(_proxies);
    }

    private async Task<IReadOnlyList<ProxyCandidate>> LoadCandidatesFromProviders()
    {
        ProxyLoader loader = new(
            _logger,
            _config,
            _proxyEndpointParserService,
            _proxyProviderTypeResolverService,
            _proxySourceLoadService
        );
        List<ProxyDataConfig> loaded = await loader.Load();
        return loaded.Select(ToCandidate).ToList();
    }

    private static ProxyRuntimeRecord ToRuntimeRecord(ProxyData data) =>
        new()
        {
            Candidate = ToCandidate(data.Proxy),
            IsActive = data.Status.Current == StatusEnum.Active,
            Connections = data.Connections
                .Select(item => new ProxyRuntimeConnection
                {
                    Date = item.Date,
                    TotalUses = item.TotalUses,
                })
                .ToList(),
            Errors = data.Errors
                .Select(item => new ProxyRuntimeError
                {
                    Short = item.Message.Short,
                    Extended = item.Message.Extended,
                    TotalDuplicates = item.TotalDuplicates,
                    Date = item.Date,
                })
                .ToList(),
        };

    private static ProxyData ToProxyData(ProxyRuntimeRecord record) =>
        new()
        {
            Proxy = ToProxyDataConfig(record.Candidate),
            Connections = record.Connections
                .Select(item => new Connection
                {
                    Date = item.Date,
                    TotalUses = item.TotalUses,
                })
                .ToList(),
            Errors = record.Errors
                .Select(item => new Error
                {
                    Message = new ErrorMessage
                    {
                        Short = item.Short,
                        Extended = item.Extended,
                    },
                    TotalDuplicates = item.TotalDuplicates,
                    Date = item.Date,
                })
                .ToList(),
            Status = new Status
            {
                Current = record.IsActive ? StatusEnum.Active : StatusEnum.Inactive,
            },
        };

    private static void ApplyRuntimeRecord(
        ProxyData proxy,
        ProxyRuntimeRecord source,
        DateTime? disabledAt,
        IProxyRuntimeStatusTransitionService transitionService
    )
    {
        proxy.Connections = source.Connections
            .Select(item => new Connection
            {
                Date = item.Date,
                TotalUses = item.TotalUses,
            })
            .ToList();
        proxy.Errors = source.Errors
            .Select(item => new Error
            {
                Message = new ErrorMessage
                {
                    Short = item.Short,
                    Extended = item.Extended,
                },
                TotalDuplicates = item.TotalDuplicates,
                Date = item.Date,
            })
            .ToList();

        ProxyRuntimeStatusTransitionResult status = transitionService.ResolveStatus(
            source.IsActive,
            proxy.Status.Date,
            disabledAt
        );
        proxy.Status.Current = status.IsActive ? StatusEnum.Active : StatusEnum.Inactive;
        if (status.StatusDate.HasValue)
            proxy.Status.Date = status.StatusDate.Value;
    }

    private static ProxyCandidate ToCandidate(ProxyDataConfig proxy) =>
        new() { Ip = proxy.Ip, Port = proxy.Port, Protocol = proxy.Protocol };

    private static ProxyDataConfig ToProxyDataConfig(ProxyCandidate candidate) =>
        new() { Ip = candidate.Ip, Port = candidate.Port, Protocol = candidate.Protocol };

    private ProxyFailureSettings BuildFailureSettings() =>
        new()
        {
            DownloadThreadStart = _config.Downloads.Threads.Start,
            AttemptsPerProxy = 3,
            ErrorsToStop = _config.Proxy.Threshold.ErrorsToStop,
        };

    private ProxyFailureState GetFailureState() =>
        new()
        {
            FailureCount = _failureCount,
            AttemptCount = count,
            StopCount = _stopCount,
            ProxyIndex = _proxyIndex,
            ProxyCount = _proxies.Count,
        };

    private void ApplyFailureState(ProxyFailureState state)
    {
        _failureCount = state.FailureCount;
        count = state.AttemptCount;
        _stopCount = state.StopCount;
        _proxyIndex = state.ProxyIndex;
    }

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
