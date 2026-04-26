using Backup.App.Models.Config.Request;
using Backup.App.Services.Post;
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
        App.Models.Config.App config = LiveApiTestSupport.LoadAppConfig();
        Request? request = RequestMerge.Build(config.Api, apiName);

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
