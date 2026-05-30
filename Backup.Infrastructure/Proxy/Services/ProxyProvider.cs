using System.Net;
using Backup.Application.Proxy;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Proxy.Models;
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
    IProxyHealthCheckPolicyService proxyHealthCheckPolicyService,
    IProxyHttpClientHeaderPolicyService proxyHttpClientHeaderPolicyService
)
    : IProxyProvider,
        ISetup,
        IDisposable
{
    private readonly ILogger<ProxyProvider> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IProxyData _data = _data;
    private readonly IProxyRuntimePolicyService _proxyRuntimePolicyService = proxyRuntimePolicyService;
    private readonly IProxyHealthCheckPolicyService _proxyHealthCheckPolicyService =
        proxyHealthCheckPolicyService;
    private readonly IProxyHttpClientHeaderPolicyService _proxyHttpClientHeaderPolicyService =
        proxyHttpClientHeaderPolicyService;

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

        ProxyLoader loader = new(_logger, _config);
        List<ProxyDataConfig> proxies = await loader.Load();

        foreach (ProxyDataConfig proxy in proxies)
        {
            if (proxiesDict.TryGetValue(proxy, out ProxyData? _))
                continue;

            proxiesDict.Add(proxy, new() { Proxy = proxy });
        }

        _proxies = [.. proxiesDict.Values];

        _proxies = _proxies
            .Where(proxy => _proxyRuntimePolicyService.ShouldIncludeInRuntimePool(
                proxy.Status.Current == StatusEnum.Active,
                proxy.Connections.Count
            ))
            .ToList();

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

        ProxyLoader loader = new(_logger, _config);
        List<ProxyDataConfig> proxiesLoader = await loader.Load();
        proxies.AddRange(proxiesLoader);
        proxies = [.. proxies.Distinct()];

        foreach (ProxyDataConfig proxy in proxies)
        {
            string url = _proxyHealthCheckPolicyService.GetHealthCheckUrl();
            HttpStatusCode? code = null;

            while (true)
            {
                try
                {
                    Uri uri = new(proxy.ToString());

                    _logger.LogInformation("Uri: {uri}", uri.ToString());

                    using HttpClientHandler handler = new()
                    {
                        Proxy = new WebProxy(uri),
                        UseProxy = true,
                        ServerCertificateCustomValidationCallback = (
                            message,
                            cert,
                            chain,
                            errors
                        ) => true,
                    };

                    using HttpClient client = new(handler)
                    {
                        Timeout = _proxyHealthCheckPolicyService.GetHealthCheckTimeout(),
                    };

                    using HttpRequestMessage requestHttp = new(HttpMethod.Get, url);

                    using HttpResponseMessage response = await client.SendAsync(
                        requestHttp,
                        HttpCompletionOption.ResponseHeadersRead
                    );

                    code = response.StatusCode;

                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error: {error}", JsonConvert.SerializeObject(ex));

                    if (_proxyHealthCheckPolicyService.ShouldFallbackToHttp(ex))
                        proxy.Protocol = "http";
                    else
                        break;
                }
            }

            if (code is not HttpStatusCode.OK)
                continue;

            ProxyData _proxy = new() { Proxy = proxy, Connections = [new() { TotalUses = 1 }] };

            if (!proxiesAdded.Add(GetProxyKey(proxy)))
                continue;

            _proxies.Add(_proxy);
            count++;

            if (count % 10 == 0)
                await SaveData();
        }

        await SaveData();
    }

    private static string GetProxyKey(ProxyDataConfig proxy) =>
        $"{proxy.Ip}:{proxy.Port}:{proxy.Protocol}".ToLowerInvariant();

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
        HttpClientHandler handler = new();

        if (_config.Proxy.Enabled)
        {
            string proxy = _proxies[_proxyIndex].Proxy.ToString();
            Uri uri = new(proxy);

            _logger.LogInformation("Uri: {uri}", uri.ToString());

            handler.Proxy = new WebProxy(uri);
            handler.UseProxy = true;
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                true;
        }

        HttpClient client = new(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(_config.Downloads.Timeout),
        };

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
            string format = "yyyy-MM-dd, HH";
            string date = DateTime.Now.ToString(format);

            Connection? conn = proxy.Connections.LastOrDefault(conn =>
                conn.Date.ToString(format) == date
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

            Error? error = proxy.Errors.LastOrDefault(error =>
                error.Message.Short == message.Short
            );

            if (error is null)
                proxy.Errors.Add(new() { Message = message });
            else
            {
                error.TotalDuplicates++;
                error.Date = DateTime.Now;
            }

            if (
                _proxyRuntimePolicyService.ShouldDisableProxy(
                    proxy.Errors.Count,
                    _config.Proxy.Threshold.ErrorsToInactive
                )
            )
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

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
