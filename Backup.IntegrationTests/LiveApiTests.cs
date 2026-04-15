using Backup.App.Models.Config;
using Backup.App.Models.Config.Request;
using Backup.App.Services.Post;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using ConfigApp = Backup.App.Models.Config.App;

namespace Backup.IntegrationTests;

public class LiveApiTests
{
    [Fact]
    [Trait("Category", "LiveApi")]
    public async Task Posts_Source_Request_Works()
    {
        if (!LiveEnabled())
            return;

        await AssertSourceRequestWorks("posts");
    }

    [Fact]
    [Trait("Category", "LiveApi")]
    public async Task Likes_Source_Request_Works()
    {
        if (!LiveEnabled())
            return;

        await AssertSourceRequestWorks("likes");
    }

    [Fact]
    [Trait("Category", "LiveApi")]
    public async Task Bookmarks_Source_Request_Works()
    {
        if (!LiveEnabled())
            return;

        await AssertSourceRequestWorks("bookmarks");
    }

    [Fact]
    [Trait("Category", "LiveApi")]
    public async Task Api_UserMedia_Request_Works()
    {
        if (!LiveEnabled())
            return;

        await AssertApiRequestWorks("UserMedia");
    }

    [Fact]
    [Trait("Category", "LiveApi")]
    public async Task Api_UsersByRestIds_Request_Works()
    {
        if (!LiveEnabled())
            return;

        await AssertApiRequestWorks("UsersByRestIds");
    }

    [Fact]
    [Trait("Category", "LiveApi")]
    public async Task Api_UserByScreenName_Request_Works()
    {
        if (!LiveEnabled())
            return;

        await AssertApiRequestWorks("UserByScreenName");
    }

    [Fact]
    [Trait("Category", "LiveApi")]
    public async Task Api_TweetDetail_Request_Works()
    {
        if (!LiveEnabled())
            return;

        await AssertApiRequestWorks("TweetDetail");
    }

    private static async Task AssertSourceRequestWorks(string sourceId)
    {
        ConfigApp config = LoadAppConfig();

        Source source =
            config.Fetch.Sources.FirstOrDefault(s =>
                string.Equals(s.Id, sourceId, StringComparison.Ordinal)
            ) ?? throw new Exception($"Source '{sourceId}' not found in App/Config/Fetch.json");

        FetchContext context = FetchContextFactory.Create(config.Fetch.Current, source);
        context.Source.Request.Query.Variables["count"] = 5;
        context.Source.Request.Query.Variables["cursor"] = null;

        PostDownloaderHttp downloader = new(NullLogger<PostDownloaderHttp>.Instance, config);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        string response = await downloader.Download(context.Source.Request, cts.Token);

        Assert.False(string.IsNullOrWhiteSpace(response));
        Assert.Contains("\"data\"", response, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertApiRequestWorks(string apiName)
    {
        ConfigApp config = LoadAppConfig();

        if (!config.Api.TryGetValue(apiName, out Request? apiRequest))
            throw new Exception($"Api '{apiName}' not found in App/Config/Api.json");

        if (!apiRequest.Enabled)
            return;

        Request request = RequestMerge.Build(config.Fetch.Current.Request, apiRequest);

        await PrepareVariables(config, request);

        PostDownloaderHttp downloader = new(NullLogger<PostDownloaderHttp>.Instance, config);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        string response = await downloader.Download(request, cts.Token);

        Assert.False(string.IsNullOrWhiteSpace(response));
        Assert.Contains("\"data\"", response, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task PrepareVariables(ConfigApp config, Request request)
    {
        string userId =
            config.Fetch.Current.Request.Query.Variables["userId"]?.ToString()
            ?? throw new Exception("userId not found in Fetch.Current.Request.Query.Variables");

        request.Query.Variables["userId"] = userId;

        if (request.Query.Variables.ContainsKey("count"))
            request.Query.Variables["count"] = 5;

        if (request.Query.Variables.ContainsKey("cursor"))
            request.Query.Variables["cursor"] = null;

        if (request.Query.Variables.ContainsKey("userIds"))
            request.Query.Variables["userIds"] = new[] { userId };

        if (request.Query.Variables.ContainsKey("screen_name"))
        {
            string? screenName = TryExtractScreenName(config.Fetch.Sources);

            if (!string.IsNullOrWhiteSpace(screenName))
                request.Query.Variables["screen_name"] = screenName;
        }

        if (request.Query.Variables.ContainsKey("focalTweetId"))
            request.Query.Variables["focalTweetId"] = await GetAnyTweetId(config);
    }

    private static string? TryExtractScreenName(IEnumerable<Source> sources)
    {
        foreach (Source source in sources)
        {
            if (!source.Request.Headers.TryGetValue("Referer", out string? referer))
                continue;

            if (string.IsNullOrWhiteSpace(referer))
                continue;

            if (!Uri.TryCreate(referer, UriKind.Absolute, out Uri? uri))
                continue;

            string[] segments = uri.AbsolutePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

            if (segments.Length == 0)
                continue;

            string first = segments[0];

            if (!string.Equals(first, "i", StringComparison.OrdinalIgnoreCase))
                return first;
        }

        return null;
    }

    private static async Task<string> GetAnyTweetId(ConfigApp config)
    {
        Source postsSource =
            config.Fetch.Sources.FirstOrDefault(s =>
                string.Equals(s.Id, "posts", StringComparison.Ordinal)
            ) ?? throw new Exception("Source 'posts' not found");

        FetchContext context = FetchContextFactory.Create(config.Fetch.Current, postsSource);
        context.Source.Request.Query.Variables["count"] = 5;
        context.Source.Request.Query.Variables["cursor"] = null;

        PostDownloaderHttp downloader = new(NullLogger<PostDownloaderHttp>.Instance, config);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        string response = await downloader.Download(context.Source.Request, cts.Token);

        JObject root = JObject.Parse(response);
        JToken? token = root.SelectToken("..entries");

        if (token is not JArray entries)
            throw new Exception("entries not found");

        foreach (JToken entry in entries)
        {
            string? entryId = entry["entryId"]?.ToString();

            if (
                !string.IsNullOrWhiteSpace(entryId)
                && entryId.StartsWith("tweet-", StringComparison.Ordinal)
            )
                return entryId["tweet-".Length..];
        }

        throw new Exception("tweet id not found in posts response");
    }

    private static bool LiveEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable("RUN_LIVE_X_API_TESTS"),
            "1",
            StringComparison.Ordinal
        );

    private static ConfigApp LoadAppConfig()
    {
        string root = FindRepositoryRoot();
        string dir = Path.Combine(root, "App", "Config");

        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Config directory not found: {dir}");

        return new ConfigApp()
        {
            Fetch = LoadFile<Fetch>(dir, "Fetch.json"),
            Api = LoadFile<Dictionary<string, Request>>(dir, "Api.json"),
            Services = LoadFile<Services>(dir, "Services.json"),
            Data = LoadFile<Backup.App.Models.Config.Data.Data>(dir, "Data.json"),
            Downloads = LoadFile<Backup.App.Models.Config.Downloads.Downloads>(
                dir,
                "Downloads.json"
            ),
            Medias = LoadFile<Backup.App.Models.Config.Medias.Medias>(dir, "Medias.json"),
            Proxy = LoadFile<Backup.App.Models.Config.Proxy.Proxy>(dir, "Proxy.json"),
            Debug = LoadFile<Debug>(dir, "Debug.json"),
            Tasks = LoadFile<Backup.App.Models.Config.Tasks.Tasks>(dir, "Tasks.json"),
            Bulk = LoadFile<Bulk>(dir, "Bulk.json"),
            Network = LoadFile<Network>(dir, "Network.json"),
        };
    }

    private static T LoadFile<T>(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        IConfigurationRoot cfg = new ConfigurationBuilder()
            .AddJsonFile(path, optional: false)
            .Build();
        return cfg.Get<T>() ?? throw new Exception($"Unable to parse '{path}'");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Backup.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new Exception("Repository root not found.");
    }
}
