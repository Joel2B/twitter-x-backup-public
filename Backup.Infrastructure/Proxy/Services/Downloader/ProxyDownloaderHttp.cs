using System.Net;
using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Proxy.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Services.Downloader;

public class ProxyDownloaderHttp(
    ILogger _logger,
    IProxyEndpointParserService proxyEndpointParserService,
    string format
) : IProxyDownloader
{
    private readonly ILogger _logger = _logger;
    private readonly IProxyEndpointParserService _proxyEndpointParserService = proxyEndpointParserService;
    private readonly string _format = format;

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

        IReadOnlyList<ProxyEndpoint> endpoints = _proxyEndpointParserService.Parse(
            _format,
            lines,
            resource.Type
        );

        return endpoints
            .Select(endpoint => new ProxyDataConfig
            {
                Ip = endpoint.Ip,
                Port = endpoint.Port,
                Protocol = endpoint.Protocol,
            })
            .ToList();
    }
}
