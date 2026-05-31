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
    IProxyRuntimeRecordMapper proxyRuntimeRecordMapper,
    IProxyRuntimePoolBuilderService proxyRuntimePoolBuilderService,
    IProxyHealthAcceptanceService proxyHealthAcceptanceService,
    IProxyRuntimeStatusTransitionService proxyRuntimeStatusTransitionService,
    IProxyFailureStateService proxyFailureStateService,
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
    private readonly IProxyRuntimeRecordMapper _proxyRuntimeRecordMapper = proxyRuntimeRecordMapper;
    private readonly IProxyRuntimePoolBuilderService _proxyRuntimePoolBuilderService =
        proxyRuntimePoolBuilderService;
    private readonly IProxyHealthAcceptanceService _proxyHealthAcceptanceService =
        proxyHealthAcceptanceService;
    private readonly IProxyRuntimeStatusTransitionService _proxyRuntimeStatusTransitionService =
        proxyRuntimeStatusTransitionService;
    private readonly IProxyFailureStateService _proxyFailureStateService = proxyFailureStateService;
    private readonly IProxyUsageTrackingService _proxyUsageTrackingService = proxyUsageTrackingService;
    private readonly IProxyErrorTrackingService _proxyErrorTrackingService = proxyErrorTrackingService;

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
        IReadOnlyList<ProxyRuntimeRecord> runtimePool = _proxyRuntimePoolBuilderService.BuildPool(
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
        IReadOnlyList<ProxyRuntimeRecord> merged = _proxyRuntimePoolBuilderService.BuildPool(
            proxiesStorage.Select(_proxyRuntimeRecordMapper.ToRuntimeRecord),
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
            _proxies.Add(_proxyRuntimeRecordMapper.ToProxyData(item.Record));

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

            ProxyFailureOutcome outcome = _proxyFailureStateService.RegisterFailure(
                BuildFailureSettings()
            );

            _logger.LogInformation("failure count: {value}", outcome.State.FailureCount);

            if (!outcome.ShouldAttemptSwitch)
                return;

            _logger.LogInformation("attempt {attempt}", outcome.State.AttemptCount);

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

        lock (proxy)
        {
            ProxyRuntimeRecord runtimeRecord = _proxyRuntimeRecordMapper.ToRuntimeRecord(proxy);
            _proxyUsageTrackingService.RegisterUse(runtimeRecord, DateTime.Now);
            _proxyRuntimeRecordMapper.ApplyRuntimeRecord(proxy, runtimeRecord, disabledAt: null);
        }

        ResetStopCount();
    }

    public void OnError(Exception ex)
    {
        if (!_config.Proxy.Enabled)
            return;

        int proxyIndex = _proxyFailureStateService.GetState().ProxyIndex;
        ProxyData proxy = _proxies[proxyIndex];

        lock (proxy)
        {
            if (
                !_proxyRuntimeStatusTransitionService.ShouldHandleError(
                    proxy.Status.Current == StatusEnum.Active
                )
            )
                return;

            DateTime now = DateTime.Now;
            ProxyRuntimeRecord runtimeRecord = _proxyRuntimeRecordMapper.ToRuntimeRecord(proxy);
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
            _proxyRuntimeRecordMapper.ApplyRuntimeRecord(proxy, runtimeRecord, disabledAt);

            if (tracking.WasDisabled)
            {
                _logger.LogInformation("proxy {proxy} disabled", proxy.Proxy.ToString());
            }
        }
    }

    private void ResetStopCount()
    {
        ProxyFailureState state = _proxyFailureStateService.GetState();

        if (state.StopCount > 0)
            _logger.LogInformation("count to stop reset");

        _proxyFailureStateService.ResetStopCount();
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

    private static ProxyCandidate ToCandidate(ProxyDataConfig proxy) =>
        new() { Ip = proxy.Ip, Port = proxy.Port, Protocol = proxy.Protocol };

    private ProxyFailureSettings BuildFailureSettings() =>
        new()
        {
            DownloadThreadStart = _config.Downloads.Threads.Start,
            AttemptsPerProxy = 3,
            ErrorsToStop = _config.Proxy.Threshold.ErrorsToStop,
        };

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
