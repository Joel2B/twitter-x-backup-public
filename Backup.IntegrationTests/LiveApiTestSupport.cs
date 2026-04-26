using Backup.App.Models.Config;
using Backup.App.Models.Config.Api;
using Backup.App.Models.Config.Request;
using Backup.App.Services.Post;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;

namespace Backup.IntegrationTests;

internal static class LiveApiTestSupport
{
    internal static async Task PrepareVariables(App.Models.Config.App config, Request request)
    {
        string userId =
            ResolveUserId(config) ?? throw new Exception("userId not found in Services.User.Id");

        if (
            request.Query.Variables.TryGetValue("userId", out object? userIdValue)
            && IsMissingOrPlaceholder(userIdValue)
        )
            request.Query.Variables["userId"] = userId;

        if (request.Query.Variables.ContainsKey("count"))
            request.Query.Variables["count"] = 5;

        if (request.Query.Variables.ContainsKey("cursor"))
            request.Query.Variables["cursor"] = null;

        if (request.Query.Variables.ContainsKey("userIds"))
            request.Query.Variables["userIds"] = new[] { userId };

        if (request.Query.Variables.ContainsKey("screen_name"))
        {
            object? currentValue = request.Query.Variables["screen_name"];
            if (IsMissingOrPlaceholder(currentValue))
            {
                string? screenName = TryExtractScreenName(config.Api.Values);

                if (!string.IsNullOrWhiteSpace(screenName))
                    request.Query.Variables["screen_name"] = screenName;
            }
        }

        if (
            request.Query.Variables.TryGetValue("focalTweetId", out object? focalTweetId)
            && IsMissingOrPlaceholder(focalTweetId)
        )
            request.Query.Variables["focalTweetId"] = await GetAnyTweetId(config, userId);
    }

    internal static string? ResolveUserId(App.Models.Config.App config)
    {
        string? configuredUserId = config.Services.User.Id?.Trim();

        if (!string.IsNullOrWhiteSpace(configuredUserId))
            return configuredUserId;

        return null;
    }

    internal static App.Models.Config.App LoadAppConfig()
    {
        return ConfigLoader.Load();
    }

    private static bool IsMissingOrPlaceholder(object? value)
    {
        string? text = value?.ToString();

        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
            return true;

        return text.Contains("{REPLACE_THIS}", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryExtractScreenName(IEnumerable<Api> apiEntries)
    {
        foreach (Api api in apiEntries)
        {
            if (!api.Request.Headers.TryGetValue("Referer", out string? referer))
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

    private static async Task<string> GetAnyTweetId(App.Models.Config.App config, string userId)
    {
        Request postsRequest =
            RequestMerge.Build(config.Api, "posts")
            ?? throw new Exception("Api 'posts' not found or disabled");

        postsRequest.Query.Variables["count"] = 5;
        postsRequest.Query.Variables["cursor"] = null;
        postsRequest.Query.Variables["userId"] = userId;

        PostDownloaderHttp downloader = new(NullLogger<PostDownloaderHttp>.Instance, config);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        string response = await downloader.Download(postsRequest, cts.Token);

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
}
