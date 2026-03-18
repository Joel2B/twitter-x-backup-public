using System.Net;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Proxy;
using Backup.App.Interfaces.Proxy;
using Backup.App.Models.Proxy;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.App.Services.Proxy;

public class ProxyException(string? message = null) : Exception(message) { }

public class ProxyEmptyException(string? message = null) : ProxyException(message) { }

public class ProxyProvider(ILogger<ProxyProvider> _logger, Models.Config.App _config, IProxyData _data)
    : IProxyProvider,
        ISetup,
        IDisposable
{
    private readonly ILogger<ProxyProvider> _logger = _logger;
    private readonly Models.Config.App _config = _config;
    private readonly IProxyData _data = _data;

    private readonly SemaphoreSlim _proxyLock = new(1);
    private int _proxyIndex = 0;
    private int _failureCount = 0;
    private int _stopCount = 0;

    private int count = 0;

    private List<Models.Proxy.Data> _proxies = [];

    private volatile HttpClient? _client;

    public async Task Setup()
    {
        await Check();

        if (!_config.Proxy.Enabled)
        {
            NewClient();

            return;
        }

        Dictionary<Models.Proxy.Proxy, Models.Proxy.Data> proxiesDict =
            await _data.GetAllAsDictionary() ?? [];

        ProxyLoader loader = new(_logger, _config);
        List<Models.Proxy.Proxy> proxies = await loader.Load();

        foreach (Models.Proxy.Proxy proxy in proxies)
        {
            if (proxiesDict.TryGetValue(proxy, out Models.Proxy.Data? _))
                continue;

            proxiesDict.Add(proxy, new() { Proxy = proxy });
        }

        _proxies = [.. proxiesDict.Values];

        _proxies = _proxies
            .Where(proxy =>
                proxy.Status.Current == StatusEnum.Active || proxy.Connections.Count > 0
            )
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

        List<Models.Proxy.Proxy> proxies = [];
        HashSet<string> proxiesAdded = [.. _proxies.Select(o => GetProxyKey(o.Proxy))];
        int count = 0;

        List<Models.Proxy.Data> proxiesStorage = await _data.GetAll() ?? [];
        proxies.AddRange(proxiesStorage.Select(o => o.Proxy));

        ProxyLoader loader = new(_logger, _config);
        List<Models.Proxy.Proxy> proxiesLoader = await loader.Load();
        proxies.AddRange(proxiesLoader);
        proxies = [.. proxies.Distinct()];

        foreach (Models.Proxy.Proxy proxy in proxies)
        {
            string url = "https://pbs.twimg.com/media/G6hPY2KbIAAm-FB?format=jpg&name=large";
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

                    using HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(10) };

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

                    if (ex.Message.Contains("The SSL connection could not be established"))
                        proxy.Protocol = "http";
                    else
                        break;
                }
            }

            if (code is not HttpStatusCode.OK)
                continue;

            Models.Proxy.Data _proxy = new()
            {
                Proxy = proxy,
                Connections = [new() { TotalUses = 1 }],
            };

            if (!proxiesAdded.Add(GetProxyKey(proxy)))
                continue;

            _proxies.Add(_proxy);
            count++;

            if (count % 10 == 0)
                await SaveData();
        }

        await SaveData();
    }

    private static string GetProxyKey(Models.Proxy.Proxy proxy) =>
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

            if (value < _config.Downloads.Threads.Start)
                return;

            await Reset();

            count++;

            _logger.LogInformation("attempt {attempt}", count);

            if (count >= 3)
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

        client.DefaultRequestHeaders.UserAgent.Clear();

        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36"
        );

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8"
        );

        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

        client.DefaultRequestHeaders.TryAddWithoutValidation("Priority", "u=0, i");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Sec-ch-ua",
            "\"Brave\";v=\"143\", \"Chromium\";v=\"143\", \"Not A(Brand\";v=\"24\""
        );

        client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-ch-ua-mobile", "?0");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-ch-ua-platform", "Windows");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-fetch-dest", "document");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-fetch-mode", "navigate");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-fetch-site", "none");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-fetch-user", "?1");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-gpc", "1");

        HttpClient? oldClient = Interlocked.Exchange(ref _client, client);
        oldClient?.Dispose();
    }

    public void OnUse()
    {
        if (!_config.Proxy.Enabled)
            return;

        Models.Proxy.Data proxy = _proxies[_proxyIndex];

        lock (proxy)
        {
            string format = "yyyy-MM-dd, HH";
            string date = DateTime.Now.ToString(format);

            Connection? conn = proxy
                .Connections.Where(conn => conn.Date.ToString(format) == date)
                .LastOrDefault();

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

        Models.Proxy.Data proxy = _proxies[_proxyIndex];

        lock (proxy)
        {
            if (proxy.Status.Current == StatusEnum.Inactive)
                return;

            ErrorMessage message = new()
            {
                Short = ex.Message,
                Extended = JsonConvert.SerializeObject(ex),
            };

            Error? error = proxy
                .Errors.Where(error => error.Message.Short == message.Short)
                .LastOrDefault();

            if (error is null)
                proxy.Errors.Add(new() { Message = message });
            else
            {
                error.TotalDuplicates++;
                error.Date = DateTime.Now;
            }

            if (proxy.Errors.Count >= _config.Proxy.Threshold.ErrorsToInactive)
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
        if (_config.Proxy.Threshold.ErrorsToStop == -1)
            return;

        int value = Interlocked.Increment(ref _stopCount);

        _logger.LogInformation(
            "{value}/{errorsToStop} count to stop",
            value,
            _config.Proxy.Threshold.ErrorsToStop
        );

        if (value < _config.Proxy.Threshold.ErrorsToStop)
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
