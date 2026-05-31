using System.Net;
using Backup.Application.Proxy;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Proxy.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Backup.Application.Proxy.Models;

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
    IProxyConnectionWindowPolicyService proxyConnectionWindowPolicyService,
    IProxyKeyPolicyService proxyKeyPolicyService,
    IProxyEndpointParserService proxyEndpointParserService,
    IProxyProviderTypeResolverService proxyProviderTypeResolverService,
    IProxySourceLoadService proxySourceLoadService,
    IProxyCandidateMergeService proxyCandidateMergeService,
    IProxyRuntimePoolSelectionService proxyRuntimePoolSelectionService,
    IProxyAcceptedCandidateFactoryService proxyAcceptedCandidateFactoryService,
    IProxyBatchFlushPolicyService proxyBatchFlushPolicyService,
    IProxyErrorDecisionService proxyErrorDecisionService
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
    private readonly IProxyConnectionWindowPolicyService _proxyConnectionWindowPolicyService =
        proxyConnectionWindowPolicyService;
    private readonly IProxyKeyPolicyService _proxyKeyPolicyService = proxyKeyPolicyService;
    private readonly IProxyEndpointParserService _proxyEndpointParserService =
        proxyEndpointParserService;
    private readonly IProxyProviderTypeResolverService _proxyProviderTypeResolverService =
        proxyProviderTypeResolverService;
    private readonly IProxySourceLoadService _proxySourceLoadService = proxySourceLoadService;
    private readonly IProxyCandidateMergeService _proxyCandidateMergeService = proxyCandidateMergeService;
    private readonly IProxyRuntimePoolSelectionService _proxyRuntimePoolSelectionService =
        proxyRuntimePoolSelectionService;
    private readonly IProxyAcceptedCandidateFactoryService _proxyAcceptedCandidateFactoryService =
        proxyAcceptedCandidateFactoryService;
    private readonly IProxyBatchFlushPolicyService _proxyBatchFlushPolicyService =
        proxyBatchFlushPolicyService;
    private readonly IProxyErrorDecisionService _proxyErrorDecisionService = proxyErrorDecisionService;

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

        Dictionary<ProxyDataConfig, ProxyData> proxiesDict = await _data.GetAllAsDictionary() ?? [];

        ProxyLoader loader = new(
            _logger,
            _config,
            _proxyEndpointParserService,
            _proxyProviderTypeResolverService,
            _proxySourceLoadService
        );
        List<ProxyDataConfig> proxies = await loader.Load();

        foreach (ProxyDataConfig proxy in proxies)
        {
            if (proxiesDict.TryGetValue(proxy, out ProxyData? _))
                continue;

            proxiesDict.Add(proxy, new() { Proxy = proxy });
        }

        _proxies = [.. proxiesDict.Values];
        _proxies = SelectRuntimePool(_proxies);

        if (_proxies.Count == 0)
            throw new ProxyEmptyException();

        await SaveData();
        NewClient();
    }

    private async Task Check()
    {
        if (!_config.Proxy.Check)
            return;

        List<ProxyDataConfig> proxies = [];
        HashSet<string> proxiesAdded = [.. _proxies.Select(o => GetProxyKey(o.Proxy))];
        int count = 0;

        List<ProxyData> proxiesStorage = await _data.GetAll() ?? [];
        proxies.AddRange(proxiesStorage.Select(o => o.Proxy));

        ProxyLoader loader = new(
            _logger,
            _config,
            _proxyEndpointParserService,
            _proxyProviderTypeResolverService,
            _proxySourceLoadService
        );
        List<ProxyDataConfig> proxiesLoader = await loader.Load();

        IReadOnlyList<ProxyCandidate> mergedCandidates = _proxyCandidateMergeService.MergeDistinct(
            proxiesStorage.Select(proxy => ToCandidate(proxy.Proxy)),
            proxiesLoader.Select(ToCandidate)
        );
        proxies = mergedCandidates.Select(ToProxyDataConfig).ToList();

        foreach (ProxyDataConfig proxy in proxies)
        {
            ProxyHealthProbeResult probe = await _proxyHealthProbeService.Probe(
                ToCandidate(proxy),
                _proxyHealthProbePort
            );

            if (probe.Error is not null)
                _logger.LogError("Error: {error}", JsonConvert.SerializeObject(probe.Error));

            if (!probe.Success)
                continue;

            ProxyAcceptedCandidate accepted = _proxyAcceptedCandidateFactoryService.Create(
                probe.Candidate
            );
            ProxyDataConfig acceptedProxy = ToProxyDataConfig(accepted.Candidate);
            ProxyData _proxy = new()
            {
                Proxy = acceptedProxy,
                Connections = [new() { TotalUses = accepted.InitialConnectionUses }],
            };

            if (!proxiesAdded.Add(GetProxyKey(acceptedProxy)))
                continue;

            _proxies.Add(_proxy);
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
            DateTime now = DateTime.Now;

            Connection? conn = proxy.Connections.LastOrDefault(conn =>
                _proxyConnectionWindowPolicyService.IsSameWindow(conn.Date, now)
            );

            if (conn is null)
            {
                conn = new();
                proxy.Connections.Add(conn);
            }

            conn.TotalUses++;
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

            ErrorMessage message = new()
            {
                Short = ex.Message,
                Extended = JsonConvert.SerializeObject(ex),
            };

            ProxyErrorDecision decision = _proxyErrorDecisionService.Decide(
                proxy.Errors.Select(item => item.Message.Short).ToList(),
                message.Short,
                _config.Proxy.Threshold.ErrorsToInactive
            );

            Error? error = proxy.Errors.LastOrDefault(item => item.Message.Short == message.Short);

            if (decision.IsNewMessage)
                proxy.Errors.Add(new() { Message = message });
            else if (error is not null)
            {
                error.TotalDuplicates++;
                error.Date = DateTime.Now;
            }

            if (decision.ShouldDisable)
            {
                proxy.Status.Current = StatusEnum.Inactive;
                proxy.Status.Date = DateTime.Now;

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

    private List<ProxyData> SelectRuntimePool(List<ProxyData> proxies)
    {
        IReadOnlySet<string> runtimeKeys = _proxyRuntimePoolSelectionService.SelectKeys(
            proxies.Select(proxy => new ProxyRuntimePoolCandidate
            {
                Key = GetProxyKey(proxy.Proxy),
                IsActive = proxy.Status.Current == StatusEnum.Active,
                ConnectionCount = proxy.Connections.Count,
            })
        );

        return proxies.Where(proxy => runtimeKeys.Contains(GetProxyKey(proxy.Proxy))).ToList();
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
