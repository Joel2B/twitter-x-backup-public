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
    IProxyRuntimePolicyService proxyRuntimePolicyService,
    IProxyHealthProbeService proxyHealthProbeService,
    IProxyHealthProbePort proxyHealthProbePort,
    IProxyHttpClientFactoryPolicyService proxyHttpClientFactoryPolicyService,
    IProxyHttpClientHeaderPolicyService proxyHttpClientHeaderPolicyService,
    IProxyKeyPolicyService proxyKeyPolicyService,
    IProxyEndpointParserService proxyEndpointParserService,
    IProxyProviderTypeResolverService proxyProviderTypeResolverService,
    IProxySourceLoadService proxySourceLoadService,
    IProxyRuntimeRecordMergeService proxyRuntimeRecordMergeService,
    IProxyRuntimePoolProjectionService proxyRuntimePoolProjectionService,
    IProxyUsageTrackingService proxyUsageTrackingService,
    IProxyErrorTrackingService proxyErrorTrackingService,
    IProxyAcceptedCandidateFactoryService proxyAcceptedCandidateFactoryService,
    IProxyBatchFlushPolicyService proxyBatchFlushPolicyService
)
    : IProxyProvider,
        ISetup,
        IDisposable
{
    private readonly ILogger<ProxyProvider> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IProxyData _data = _data;
    private readonly IProxyRuntimePolicyService _proxyRuntimePolicyService = proxyRuntimePolicyService;
    private readonly IProxyHealthProbeService _proxyHealthProbeService = proxyHealthProbeService;
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
    private readonly IProxyRuntimeRecordMergeService _proxyRuntimeRecordMergeService =
        proxyRuntimeRecordMergeService;
    private readonly IProxyRuntimePoolProjectionService _proxyRuntimePoolProjectionService =
        proxyRuntimePoolProjectionService;
    private readonly IProxyUsageTrackingService _proxyUsageTrackingService = proxyUsageTrackingService;
    private readonly IProxyErrorTrackingService _proxyErrorTrackingService = proxyErrorTrackingService;
    private readonly IProxyAcceptedCandidateFactoryService _proxyAcceptedCandidateFactoryService =
        proxyAcceptedCandidateFactoryService;
    private readonly IProxyBatchFlushPolicyService _proxyBatchFlushPolicyService =
        proxyBatchFlushPolicyService;

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
        IReadOnlyList<ProxyRuntimeRecord> merged = _proxyRuntimeRecordMergeService.MergeStoredAndLoaded(
            stored.Select(ToRuntimeRecord),
            await LoadCandidatesFromProviders()
        );
        IReadOnlyList<ProxyRuntimeRecord> runtimePool = _proxyRuntimePoolProjectionService.SelectPool(
            merged
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
        int count = 0;

        List<ProxyData> proxiesStorage = await _data.GetAll() ?? [];
        IReadOnlyList<ProxyRuntimeRecord> merged = _proxyRuntimeRecordMergeService.MergeStoredAndLoaded(
            proxiesStorage.Select(ToRuntimeRecord),
            await LoadCandidatesFromProviders()
        );

        foreach (ProxyRuntimeRecord record in merged)
        {
            ProxyHealthProbeResult probe = await _proxyHealthProbeService.Probe(
                record.Candidate,
                _proxyHealthProbePort
            );

            if (probe.Error is not null)
                _logger.LogError("Error: {error}", JsonConvert.SerializeObject(probe.Error));

            if (!probe.Success)
                continue;

            ProxyAcceptedCandidate accepted = _proxyAcceptedCandidateFactoryService.Create(
                probe.Candidate
            );
            ProxyRuntimeRecord acceptedRecord = new()
            {
                Candidate = accepted.Candidate,
                Connections =
                [
                    new ProxyRuntimeConnection
                    {
                        TotalUses = accepted.InitialConnectionUses,
                    },
                ],
            };
            ProxyData proxyData = ToProxyData(acceptedRecord);
            ProxyDataConfig acceptedProxy = proxyData.Proxy;

            if (!proxiesAdded.Add(GetProxyKey(acceptedProxy)))
                continue;

            _proxies.Add(proxyData);
            count++;

            if (_proxyBatchFlushPolicyService.ShouldFlush(count, flushEvery: 10))
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

            int value = Interlocked.Increment(ref _failureCount);

            _logger.LogInformation("failure count: {value}", value);

            if (
                !_proxyRuntimePolicyService.ShouldAttemptProxySwitch(
                    value,
                    _config.Downloads.Threads.Start
                )
            )
                return;

            await Reset();

            count++;

            _logger.LogInformation("attempt {attempt}", count);

            if (_proxyRuntimePolicyService.ShouldRotateProxy(count, attemptsPerProxy: 3))
            {
                count = 0;

                _proxyIndex++;

                if (_proxyIndex >= _proxies.Count)
                    throw new ProxyEmptyException();

                CheckStop();
                NewClient();
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

        Interlocked.Exchange(ref _failureCount, 0);

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
            ApplyRuntimeRecord(proxy, runtimeRecord, disabledAt: null);
        }

        ResetStop();
    }

    public void OnError(Exception ex)
    {
        if (!_config.Proxy.Enabled)
            return;

        ProxyData proxy = _proxies[_proxyIndex];

        lock (proxy)
        {
            if (proxy.Status.Current == StatusEnum.Inactive)
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
            ApplyRuntimeRecord(proxy, runtimeRecord, tracking.WasDisabled ? now : null);

            if (tracking.WasDisabled)
            {
                _logger.LogInformation("proxy {proxy} disabled", proxy.Proxy.ToString());
            }
        }
    }

    private void ResetStop()
    {
        if (_stopCount > 0)
            _logger.LogInformation("count to stop reset");

        Interlocked.Exchange(ref _stopCount, 0);
    }

    private void CheckStop()
    {
        if (
            _proxyRuntimePolicyService.IsStopThresholdDisabled(
                _config.Proxy.Threshold.ErrorsToStop
            )
        )
            return;

        int value = Interlocked.Increment(ref _stopCount);

        _logger.LogInformation(
            "{value}/{errorsToStop} count to stop",
            value,
            _config.Proxy.Threshold.ErrorsToStop
        );

        if (
            !_proxyRuntimePolicyService.ShouldStopProcess(
                value,
                _config.Proxy.Threshold.ErrorsToStop
            )
        )
            return;

        throw new ProxyException();
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
        DateTime? disabledAt
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

        if (source.IsActive)
            proxy.Status.Current = StatusEnum.Active;
        else
        {
            proxy.Status.Current = StatusEnum.Inactive;
            if (disabledAt.HasValue)
                proxy.Status.Date = disabledAt.Value;
        }
    }

    private static ProxyCandidate ToCandidate(ProxyDataConfig proxy) =>
        new() { Ip = proxy.Ip, Port = proxy.Port, Protocol = proxy.Protocol };

    private static ProxyDataConfig ToProxyDataConfig(ProxyCandidate candidate) =>
        new() { Ip = candidate.Ip, Port = candidate.Port, Protocol = candidate.Protocol };

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
