using Backup.App.Models.Config.Api;
using Backup.App.Models.Config.Request;
using Backup.App.Services.Post;
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
        App.Models.Config.App config = LiveApiTestSupport.LoadAppConfig();

        if (!config.Api.TryGetValue(apiId, out Api? apiEntry))
            throw new Exception($"Api '{apiId}' not found in App/Config/Api.json");

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
            ?? throw new Exception("Unable to resolve userId from Services.User.Id");

        request.Query.Variables["userId"] = userId;

        PostDownloaderHttp downloader = new(NullLogger<PostDownloaderHttp>.Instance, config);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        string response = await downloader.Download(request, cts.Token);

        Assert.False(string.IsNullOrWhiteSpace(response));
        Assert.Contains("\"data\"", response, StringComparison.OrdinalIgnoreCase);
        Assert.True(request.Query.Variables.ContainsKey("count"));
    }
}
