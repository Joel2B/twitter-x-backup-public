using Backup.App.Models.Config;
using Backup.App.Models.Config.Request;
using Microsoft.Extensions.Configuration;

namespace Backup.Tests;

public class ConfigSourceMergeTests
{
    [Fact]
    public void Create_CombinesCurrentAndSourceValues()
    {
        Source current = new()
        {
            Id = "current",
            Enabled = true,
            Count = 500,
            Request = new()
            {
                Url = "https://x.com/i/api/graphql/current/UserTweets",
                Query = new()
                {
                    Variables = new Dictionary<string, object?>
                    {
                        ["userId"] = "1122205668801257472",
                        ["count"] = "500",
                        ["cursor"] = null,
                    },
                    Features = new Dictionary<string, bool> { ["featureA"] = true },
                    FieldToggles = new Dictionary<string, bool>
                    {
                        ["withArticlePlainText"] = false,
                    },
                },
                Headers = new Dictionary<string, string>
                {
                    ["authorization"] = "Bearer base",
                    ["cookie"] = "cookie-base",
                    ["Referer"] = "https://x.com/base",
                },
            },
        };

        Source source = new()
        {
            Id = "likes",
            Enabled = true,
            Count = 100,
            Request = new()
            {
                Url = "https://x.com/i/api/graphql/likes/Likes",
                Query = new()
                {
                    Variables = new Dictionary<string, object?>
                    {
                        ["count"] = "100",
                        ["cursor"] = "CURSOR_1",
                    },
                    Features = new Dictionary<string, bool> { ["featureB"] = false },
                    FieldToggles = new Dictionary<string, bool> { ["newToggle"] = true },
                },
                Headers = new Dictionary<string, string>
                {
                    ["Referer"] = "https://x.com/i/likes",
                    ["x-client-transaction-id"] = "txn-1",
                },
            },
        };

        FetchContext context = FetchContextFactory.Create(current, source);
        Source merged = context.Source;

        Assert.Equal("likes", merged.Id);
        Assert.Equal(100, merged.Count);
        Assert.Equal("https://x.com/i/api/graphql/likes/Likes", merged.Request.Url);

        Assert.Equal("1122205668801257472", merged.Request.Query.Variables["userId"]?.ToString());
        Assert.Equal(100, merged.Request.Query.Variables["count"]);
        Assert.Equal("CURSOR_1", merged.Request.Query.Variables["cursor"]);

        Assert.True(merged.Request.Query.Features["featureA"]);
        Assert.False(merged.Request.Query.Features["featureB"]);
        Assert.False(merged.Request.Query.FieldToggles["withArticlePlainText"]);
        Assert.True(merged.Request.Query.FieldToggles["newToggle"]);

        Assert.Equal("Bearer base", merged.Request.Headers["authorization"]);
        Assert.Equal("cookie-base", merged.Request.Headers["cookie"]);
        Assert.Equal("https://x.com/i/likes", merged.Request.Headers["Referer"]);
        Assert.Equal("txn-1", merged.Request.Headers["x-client-transaction-id"]);
    }

    [Fact]
    public void Create_DoesNotMutateInputCurrentSource()
    {
        Source current = new()
        {
            Id = "current",
            Enabled = true,
            Count = 500,
            Request = new()
            {
                Url = "https://x.com/i/api/graphql/current/UserTweets",
                Query = new()
                {
                    Variables = new Dictionary<string, object?>
                    {
                        ["userId"] = "1122205668801257472",
                        ["count"] = "500",
                        ["cursor"] = null,
                    },
                    Features = new Dictionary<string, bool> { ["featureA"] = true },
                    FieldToggles = new Dictionary<string, bool>
                    {
                        ["withArticlePlainText"] = false,
                    },
                },
                Headers = new Dictionary<string, string>
                {
                    ["authorization"] = "Bearer base",
                    ["cookie"] = "cookie-base",
                },
            },
        };

        Source source = new()
        {
            Id = "likes",
            Enabled = true,
            Count = 100,
            Request = new()
            {
                Url = "https://x.com/i/api/graphql/likes/Likes",
                Query = new()
                {
                    Variables = new Dictionary<string, object?>
                    {
                        ["count"] = "100",
                        ["cursor"] = "CURSOR_1",
                    },
                    Features = new Dictionary<string, bool> { ["featureB"] = false },
                    FieldToggles = new Dictionary<string, bool> { ["newToggle"] = true },
                },
                Headers = new Dictionary<string, string>
                {
                    ["Referer"] = "https://x.com/i/likes",
                    ["x-client-transaction-id"] = "txn-1",
                },
            },
        };

        Source currentSnapshot = current.Clone();
        Source sourceSnapshot = source.Clone();

        FetchContext context = FetchContextFactory.Create(current, source);
        context.Source.Request.Query.Variables["cursor"] = "CURSOR_CHANGED";
        context.Source.Request.Headers["cookie"] = "cookie-changed";

        Assert.Equal(
            currentSnapshot.Request.Query.Variables["cursor"],
            current.Request.Query.Variables["cursor"]
        );

        Assert.Equal(currentSnapshot.Request.Headers["cookie"], current.Request.Headers["cookie"]);

        Assert.Equal(
            sourceSnapshot.Request.Query.Variables["cursor"],
            source.Request.Query.Variables["cursor"]
        );

        Assert.Equal(
            sourceSnapshot.Request.Headers["x-client-transaction-id"],
            source.Request.Headers["x-client-transaction-id"]
        );
    }

    [Fact]
    public void RequestMerge_Build_MergesAndDoesNotMutateInputs()
    {
        Request current = new()
        {
            Url = "https://x.com/base",
            Query = new()
            {
                Variables = new Dictionary<string, object?>
                {
                    ["count"] = "500",
                    ["cursor"] = null,
                },
                Features = new Dictionary<string, bool> { ["f1"] = true },
                FieldToggles = new Dictionary<string, bool> { ["t1"] = false },
            },
            Headers = new Dictionary<string, string>
            {
                ["authorization"] = "Bearer base",
                ["cookie"] = "cookie-base",
            },
        };

        Request source = new()
        {
            Url = "https://x.com/override",
            Query = new()
            {
                Variables = new Dictionary<string, object?>
                {
                    ["count"] = "100",
                    ["cursor"] = "NEXT",
                },
                Features = new Dictionary<string, bool> { ["f2"] = false },
                FieldToggles = new Dictionary<string, bool> { ["t2"] = true },
            },
            Headers = new Dictionary<string, string> { ["Referer"] = "https://x.com/i/media" },
        };

        Request currentSnapshot = current.Clone();
        Request sourceSnapshot = source.Clone();

        Request merged = RequestMerge.Build(current, source);

        Assert.Equal("https://x.com/override", merged.Url);
        Assert.Equal(100, merged.Query.Variables["count"]);
        Assert.Equal("NEXT", merged.Query.Variables["cursor"]);
        Assert.True(merged.Query.Features["f1"]);
        Assert.False(merged.Query.Features["f2"]);
        Assert.False(merged.Query.FieldToggles["t1"]);
        Assert.True(merged.Query.FieldToggles["t2"]);
        Assert.Equal("Bearer base", merged.Headers["authorization"]);
        Assert.Equal("cookie-base", merged.Headers["cookie"]);
        Assert.Equal("https://x.com/i/media", merged.Headers["Referer"]);

        Assert.Equal(currentSnapshot.Url, current.Url);
        Assert.Equal(currentSnapshot.Query.Variables["count"], current.Query.Variables["count"]);
        Assert.Equal(sourceSnapshot.Query.Variables["count"], source.Query.Variables["count"]);
    }

    [Fact]
    public void Create_UsesFetchSources_PostsLikesBookmarks_FromProdAndExampleConfig()
    {
        string root = FindRepositoryRoot();
        string[] fetchPaths =
        [
            Path.Combine(root, "App", "Config", "Fetch.json"),
            Path.Combine(root, "App", "Config.example", "Fetch.json"),
        ];

        var expectedBySource = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["posts"] = "/UserTweets",
            ["likes"] = "/Likes",
            ["bookmarks"] = "/Bookmarks",
        };

        int validatedConfigs = 0;

        foreach (string fetchPath in fetchPaths.Where(File.Exists))
        {
            IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile(fetchPath).Build();
            Fetch fetch =
                config.Get<Fetch>() ?? throw new Exception($"Unable to load '{fetchPath}'");

            foreach (Source source in fetch.Sources.Where(s => expectedBySource.ContainsKey(s.Id)))
            {
                FetchContext context = FetchContextFactory.Create(fetch.Current, source);
                Source merged = context.Source;

                Assert.Equal(source.Id, merged.Id);
                Assert.Equal(source.Count, merged.Count);

                Assert.EndsWith(
                    expectedBySource[source.Id],
                    merged.Request.Url,
                    StringComparison.Ordinal
                );

                Assert.True(merged.Request.Query.Variables.ContainsKey("userId"));
                Assert.True(merged.Request.Query.Variables.ContainsKey("count"));
                Assert.True(merged.Request.Query.Variables.ContainsKey("cursor"));

                Assert.True(merged.Request.Headers.ContainsKey("authorization"));
                Assert.True(merged.Request.Headers.ContainsKey("cookie"));
                Assert.True(merged.Request.Headers.ContainsKey("x-client-transaction-id"));
                Assert.True(merged.Request.Headers.ContainsKey("Referer"));
            }

            validatedConfigs++;
        }

        Assert.True(
            validatedConfigs > 0,
            "No Fetch.json found in App/Config or App/Config.example."
        );
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
