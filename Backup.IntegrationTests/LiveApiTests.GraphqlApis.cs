using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.ApiRequest;
using Backup.Infrastructure.Services.Posts;
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
        Request? request = RequestMerge.Build(primaryApi, apiName);

        if (request is null)
            return;

        await LiveApiTestSupport.PrepareVariables(config, request);

        PostDownloaderHttp downloader = new(NullLogger<PostDownloaderHttp>.Instance, config);
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        string response = await downloader.Download(request, cts.Token);

        Assert.False(string.IsNullOrWhiteSpace(response));
        Assert.Contains("\"data\"", response, StringComparison.OrdinalIgnoreCase);
    }
}

