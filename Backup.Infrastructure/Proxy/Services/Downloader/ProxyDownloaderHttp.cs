using System.Net;
using Backup.Infrastructure.Interfaces.Proxy;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Models.Proxy;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Proxy.Downloader;

public class ProxyDownloaderHttp(ILogger _logger, IProxyFormatter _formatter) : IProxyDownloader
{
    private readonly ILogger _logger = _logger;
    private readonly IProxyFormatter _formatter = _formatter;

    private readonly HttpClient _client = new();
    private readonly CancellationTokenSource _tokenSource = new();

    public async Task<List<ProxyDataConfig>?> Load(Resource resource)
    {
        using HttpRequestMessage requestHttp = new()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(resource.Value),
        };

        using HttpResponseMessage response = await _client.SendAsync(
            requestHttp,
            _tokenSource.Token
        );

        HttpStatusCode code = response.StatusCode;

        if (code is not HttpStatusCode.OK)
        {
            _logger.LogInformation(code.ToString());
            return null;
        }

        string content = await response.Content.ReadAsStringAsync(_tokenSource.Token);

        List<string> lines = content
            .Split("\r\n")
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        List<ProxyDataConfig>? proxies = _formatter.Load(lines, resource.Type);

        return proxies;
    }
}


