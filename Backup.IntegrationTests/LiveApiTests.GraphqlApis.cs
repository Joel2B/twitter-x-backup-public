using Backup.Application.Core;
using Backup.Application.Network;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.IntegrationTests;

public partial class LiveApiTests
{
    [LiveApiFact]
    [Trait("Category", "LiveApi")]
    public async Task Api_UserMedia_Request_Works()
    {
        await AssertApiRequestWorks("UserMedia");
    }

    [LiveApiFact]
    [Trait("Category", "LiveApi")]
    public async Task Api_UsersByRestIds_Request_Works()
    {
        await AssertApiRequestWorks("UsersByRestIds");
    }

    [LiveApiFact]
    [Trait("Category", "LiveApi")]
    public async Task Api_UserByScreenName_Request_Works()
    {
        await AssertApiRequestWorks("UserByScreenName");
    }

    [LiveApiFact]
    [Trait("Category", "LiveApi")]
    public async Task Api_TweetDetail_Request_Works()
    {
        await AssertApiRequestWorks("TweetDetail");
    }

    private static async Task AssertApiRequestWorks(string apiName)
    {
        AppConfig config = LiveApiTestSupport.LoadAppConfig();
        IReadOnlyDictionary<string, ApiConfig> primaryApi = config.UsersContext[0].Api;

        if (!primaryApi.TryGetValue(apiName, out ApiConfig? apiEntry))
            throw new Exception($"Api '{apiName}' not found in configured api map");

        if (!apiEntry.Enabled)
            return;

        Request request = apiEntry.Request.Clone();

        await LiveApiTestSupport.PrepareVariables(config, request);

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
    }
}
