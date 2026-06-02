using System.Net;
using System.Net.Http.Headers;
using Backup.Application.Core;
using Backup.Application.Network;
using Backup.Application.Network.Models;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Request;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Adapters;

public class PostDownloaderHttp(
    ILogger<PostDownloaderHttp> _logger,
    AppConfig _config,
    IHttpRequestHeaderPolicyService httpRequestHeaderPolicyService,
    IRateLimitHeaderParserService rateLimitHeaderParserService,
    IRateLimitDecisionService rateLimitDecisionService,
    IRetryDelayPolicyService retryDelayPolicyService,
    IRequestQueryStringPolicyService requestQueryStringPolicyService,
    IDateTimeProvider dateTimeProvider
) : IPostDownloader
{
    private readonly ILogger<PostDownloaderHttp> _logger = _logger;

    private readonly AppConfig _config = _config;
    private readonly IHttpRequestHeaderPolicyService _httpRequestHeaderPolicyService =
        httpRequestHeaderPolicyService;
    private readonly IRateLimitHeaderParserService _rateLimitHeaderParserService =
        rateLimitHeaderParserService;
    private readonly IRateLimitDecisionService _rateLimitDecisionService = rateLimitDecisionService;
    private readonly IRetryDelayPolicyService _retryDelayPolicyService = retryDelayPolicyService;
    private readonly IRequestQueryStringPolicyService _requestQueryStringPolicyService =
        requestQueryStringPolicyService;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
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

        string url = _requestQueryStringPolicyService.Build(
            request.Url,
            request.Query.Variables,
            request.Query.Features,
            request.Query.FieldToggles
        );

        using HttpRequestMessage requestHttp = new()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url),
        };

        _httpRequestHeaderPolicyService.ApplyHeaders(requestHttp, request.Headers);

        _logger.LogInformation("request url: {url}", url);

        _logger.LogInformation(
            "request headers: {headers}",
            JsonConvert.SerializeObject(request.Headers, Formatting.None)
        );

        using HttpResponseMessage response = await _client.SendAsync(requestHttp, token);
        HttpStatusCode code = response.StatusCode;

        if (code is not HttpStatusCode.OK)
            throw new HttpRequestException(
                $"Post download request failed with status code {(int)code} ({code}).",
                inner: null,
                statusCode: code
            );

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

        RateLimitHeaders parsedHeaders = _rateLimitHeaderParserService.Parse(
            rawLimit?.FirstOrDefault(),
            rawRemaining?.FirstOrDefault(),
            rawReset?.FirstOrDefault()
        );

        int limit = parsedHeaders.Limit;
        int remaining = parsedHeaders.Remaining;
        int reset = parsedHeaders.ResetUnixSeconds;

        DateTimeOffset resetAt = DateTimeOffset.FromUnixTimeSeconds(parsedHeaders.ResetUnixSeconds);
        DateTimeOffset now = _dateTimeProvider.Now;
        TimeSpan diff = resetAt - now;
        RateLimitDecision decision = _rateLimitDecisionService.Evaluate(
            limit,
            remaining,
            _config.Network.RateLimit.ThresholdRemaining,
            _config.Network.RateLimit.Wait.Reset,
            now,
            resetAt
        );

        _logger.LogInformation("URL: {url}", _request.Url);
        _logger.LogInformation("limit: {limit}", limit);
        _logger.LogInformation(
            "remaining: {limit}, threshold: {threshold}, diff: {diff}",
            remaining,
            decision.Threshold,
            remaining - decision.Threshold
        );
        _logger.LogInformation("reset: {limit}, diff: {diff}", reset, diff);

        if (!decision.Continue)
            return false;

        if (decision.WaitMilliseconds > 0)
        {
            await Task.Delay(decision.WaitMilliseconds);
            return true;
        }

        await Delay();
        return true;
    }

    private async Task Delay()
    {
        int ms = _retryDelayPolicyService.GetDelayMilliseconds(
            _config.Network.RateLimit.Wait.Min,
            _config.Network.RateLimit.Wait.Max
        );
        _logger.LogInformation("delay: {delay} ms", ms);

        await Task.Delay(ms);
    }
}
