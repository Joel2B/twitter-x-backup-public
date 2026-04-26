using System.Net;
using System.Net.Http.Headers;
using Backup.App.Interfaces.Services.Post;
using Backup.App.Models.Config.Request;
using Backup.App.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.App.Services.Post;

public class PostDownloaderHttp(ILogger<PostDownloaderHttp> _logger, Models.Config.App _config)
    : IPostDownloader
{
    private readonly ILogger<PostDownloaderHttp> _logger = _logger;

    private readonly Models.Config.App _config = _config;
    private readonly HttpClient _client = new(
        new HttpClientHandler
        {
            UseCookies = false,
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli,
        }
    );

    private Request? _request;
    private HttpResponseHeaders? _headers;

    public async Task<string> Download(Request request, CancellationToken token)
    {
        _request = null;
        _headers = null;

        List<string> keysToRemove = request
            .Query.Variables.Where(kv => kv.Value == null)
            .Select(kv => kv.Key)
            .ToList();

        foreach (string key in keysToRemove)
            request.Query.Variables.Remove(key);

        Dictionary<string, string> queryBuilder = new()
        {
            { "variables", JsonConvert.SerializeObject(request.Query.Variables) },
            { "features", JsonConvert.SerializeObject(request.Query.Features) },
            { "fieldToggles", JsonConvert.SerializeObject(request.Query.FieldToggles) },
        };

        string queryUri = queryBuilder.Aggregate(
            "?",
            (query, current) => $"{query}{current.Key}={Uri.EscapeDataString(current.Value)}&"
        )[..^1];

        string url = $"{request.Url}{queryUri}";

        using HttpRequestMessage requestHttp = new()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url),
        };

        Http.ApplyHeaders(requestHttp, request.Headers);

        using HttpResponseMessage response = await _client.SendAsync(requestHttp, token);
        HttpStatusCode code = response.StatusCode;

        if (code is not HttpStatusCode.OK)
            throw new Exception(code.ToString());

        _headers = response.Headers;
        _request = request.Clone();

        string content = await response.Content.ReadAsStringAsync(token);

        return content;
    }

    public async Task<bool> Verify()
    {
        if (!_config.Network.RateLimit.Enabled || _request is null || _headers is null)
            return true;

        if (!_config.Bulk.Enabled)
        {
            await Delay();
            return true;
        }

        _headers.TryGetValues("x-rate-limit-limit", out var rawLimit);
        _headers.TryGetValues("x-rate-limit-remaining", out var rawRemaining);
        _headers.TryGetValues("x-rate-limit-reset", out var rawReset);

        if (!int.TryParse(rawLimit?.FirstOrDefault(), out var limit))
            throw new Exception("no limit");

        if (!int.TryParse(rawRemaining?.FirstOrDefault(), out var remaining))
            throw new Exception("no remaining");

        if (!int.TryParse(rawReset?.FirstOrDefault(), out var reset))
            throw new Exception("no reset");

        TimeSpan diff = DateTimeOffset.FromUnixTimeSeconds(reset) - DateTimeOffset.Now;
        int threshold = _config.Network.RateLimit.ThresholdRemaining * limit / 100;

        _logger.LogInformation("URL: {url}", _request.Url);
        _logger.LogInformation("limit: {limit}", limit);
        _logger.LogInformation(
            "remaining: {limit}, threshold: {threshold}, diff: {diff}",
            remaining,
            threshold,
            remaining - threshold
        );
        _logger.LogInformation("reset: {limit}, diff: {diff}", reset, diff);

        if (remaining <= threshold)
        {
            if (_config.Network.RateLimit.Wait.Reset)
            {
                await Task.Delay((int)Math.Max(0, diff.TotalSeconds) * 1000);
                return true;
            }

            return false;
        }

        await Delay();
        return true;
    }

    private async Task Delay()
    {
        int min = Math.Max(1, _config.Network.RateLimit.Wait.Min);
        int max = Math.Max(min, _config.Network.RateLimit.Wait.Max);

        int ms = Random.Shared.Next(min * 1000, max * 1000 + 1);
        _logger.LogInformation("delay: {delay} ms", ms);

        await Task.Delay(ms);
    }
}
