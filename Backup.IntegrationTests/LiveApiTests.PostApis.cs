using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Application.Core;
using Backup.Application.Network;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.IntegrationTests;

public partial class LiveApiTests
{
    [LiveApiFact]
    [Trait("Category", "LiveApi")]
    public async Task Posts_Api_Request_Works()
    {
        await AssertPostApiRequestWorks("posts");
    }

    [LiveApiFact]
    [Trait("Category", "LiveApi")]
    public async Task Likes_Api_Request_Works()
    {
        await AssertPostApiRequestWorks("likes");
    }

    [LiveApiFact]
    [Trait("Category", "LiveApi")]
    public async Task Bookmarks_Api_Request_Works()
    {
        await AssertPostApiRequestWorks("bookmarks");
    }

    private static async Task AssertPostApiRequestWorks(string apiId)
    {
        AppConfig config = LiveApiTestSupport.LoadAppConfig();
        IReadOnlyDictionary<string, ApiConfig> primaryApi = config.UsersContext[0].Api;

        if (!primaryApi.TryGetValue(apiId, out ApiConfig? apiEntry))
            throw new Exception($"Api '{apiId}' not found in configured api map");

        if (!apiEntry.Enabled)
            return;

        Request request = apiEntry.Request.Clone();
        request.Query.Variables["count"] = 5;

        if (request.Query.Variables.ContainsKey("cursor"))
            request.Query.Variables["cursor"] = null;

        string? requestUserId = request.Query.Variables.TryGetValue("userId", out object? userValue)
            ? userValue?.ToString()
            : null;

        string userId =
            requestUserId
            ?? LiveApiTestSupport.ResolveUserId(config)
            ?? throw new Exception("Unable to resolve userId from Services.Users");

        request.Query.Variables["userId"] = userId;

        PostDownloaderHttp downloader = new(
            NullLogger<PostDownloaderHttp>.Instance,
            config,
            new HttpRequestHeaderPolicyService(),
            new RateLimitHeaderParserService(),
            new RateLimitDecisionService(),
            new RetryDelayPolicyService(),
            new RequestQueryStringPolicyService(),
            new DateTimeProvider()
        );

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        string response = await downloader.Download(request, cts.Token);

        Assert.False(string.IsNullOrWhiteSpace(response));
        Assert.Contains("\"data\"", response, StringComparison.OrdinalIgnoreCase);
        Assert.True(request.Query.Variables.ContainsKey("count"));
    }
}
