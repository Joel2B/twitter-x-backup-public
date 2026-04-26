using Backup.App.Models.Config;
using Backup.App.Models.Config.Api;

namespace Backup.Tests;

public partial class ConfigApiTests
{
    [Fact]
    public void Fetch_Config_HasPostsLikesBookmarks_InProdAndExample()
    {
        string root = ConfigApiTestSupport.FindRepositoryRoot();

        string[] paths =
        [
            Path.Combine(root, "App", "Config", "Fetch.json"),
            Path.Combine(root, "App", "Config.example", "Fetch.json"),
        ];

        int validatedConfigs = 0;

        foreach (string path in paths)
        {
            if (!File.Exists(path))
                continue;

            Dictionary<string, FetchItem> fetch = ConfigApiTestSupport.LoadFile<
                Dictionary<string, FetchItem>
            >(path);

            Assert.True(fetch.ContainsKey("posts"), $"{path} missing posts");
            Assert.True(fetch.ContainsKey("likes"), $"{path} missing likes");
            Assert.True(fetch.ContainsKey("bookmarks"), $"{path} missing bookmarks");

            foreach (var kvp in fetch)
            {
                Assert.NotNull(kvp.Value);
                Assert.True(kvp.Value.Count > 0, $"{path}: '{kvp.Key}' Count must be > 0");
                Assert.True(kvp.Value.Api > 0, $"{path}: '{kvp.Key}' Api must be > 0");
            }

            validatedConfigs++;
        }

        Assert.True(
            validatedConfigs > 0,
            "No Fetch.json found in App/Config or App/Config.example."
        );
    }

    [Fact]
    public void ConfigLoader_AppliesFetchApiCount_IntoFinalApi()
    {
        string root = ConfigApiTestSupport.FindRepositoryRoot();
        string[] directories =
        [
            Path.Combine(root, "App", "Config"),
            Path.Combine(root, "App", "Config.example"),
        ];

        int validatedConfigs = 0;

        foreach (string directory in directories)
        {
            string apiPath = Path.Combine(directory, "Api.json");
            string fetchPath = Path.Combine(directory, "Fetch.json");

            if (!File.Exists(apiPath) || !File.Exists(fetchPath))
                continue;

            Dictionary<string, FetchItem> expectedFetch = ConfigApiTestSupport.LoadFile<
                Dictionary<string, FetchItem>
            >(fetchPath);
            App.Models.Config.App loaded = ConfigApiTestSupport.LoadSplit(directory);

            foreach (var kvp in expectedFetch)
            {
                string apiId = kvp.Key;
                FetchItem expected = kvp.Value;

                Assert.True(
                    loaded.Api.ContainsKey(apiId),
                    $"{directory}: Api '{apiId}' not found in loaded config"
                );
                Assert.True(
                    loaded.Fetch.ContainsKey(apiId),
                    $"{directory}: Fetch '{apiId}' not found in loaded config"
                );

                FetchItem actualFetch = loaded.Fetch[apiId];
                Assert.Equal(expected.Count, actualFetch.Count);
                Assert.Equal(expected.Api, actualFetch.Api);

                Api api = loaded.Api[apiId];
                Assert.True(
                    api.Request.Query.Variables.ContainsKey("count"),
                    $"{directory}: api '{apiId}' missing query.variables.count"
                );
                Assert.Equal(expected.Api, (int)api.Request.Query.Variables["count"]!);

                Assert.True(
                    api.Request.Query.Variables.ContainsKey("cursor"),
                    $"{directory}: api '{apiId}' expected query.variables.cursor"
                );
                Assert.Null(api.Request.Query.Variables["cursor"]);
            }

            validatedConfigs++;
        }

        Assert.True(
            validatedConfigs > 0,
            "No config directories with Api.json + Fetch.json were found."
        );
    }

    [Fact]
    public void ConfigLoader_NormalizesApiCountVariables_ToInt()
    {
        string root = ConfigApiTestSupport.FindRepositoryRoot();

        string[] directories =
        [
            Path.Combine(root, "App", "Config"),
            Path.Combine(root, "App", "Config.example"),
        ];

        int validatedConfigs = 0;

        foreach (string directory in directories)
        {
            string apiPath = Path.Combine(directory, "Api.json");

            if (!File.Exists(apiPath))
                continue;

            App.Models.Config.App loaded = ConfigApiTestSupport.LoadSplit(directory);

            foreach (var kvp in loaded.Api)
            {
                Api api = kvp.Value;

                if (!api.Request.Query.Variables.TryGetValue("count", out object? count))
                    continue;

                Assert.True(
                    count is int,
                    $"{directory}: api '{api.Id}' expected int for query.variables.count, got '{count?.GetType().Name ?? "null"}'"
                );

                int value = (int)count!;

                Assert.True(
                    value > 0 || value == -1,
                    $"{directory}: api '{api.Id}' invalid query.variables.count value '{value}'"
                );

                Assert.True(
                    api.Request.Query.Variables.ContainsKey("cursor"),
                    $"{directory}: api '{api.Id}' expected query.variables.cursor when count exists"
                );

                Assert.Null(api.Request.Query.Variables["cursor"]);
            }

            foreach (var kvp in loaded.Fetch)
            {
                Assert.True(
                    kvp.Value.Count > 0,
                    $"{directory}: fetch '{kvp.Key}' invalid Count '{kvp.Value.Count}'"
                );
                Assert.True(
                    kvp.Value.Api > 0,
                    $"{directory}: fetch '{kvp.Key}' invalid Api '{kvp.Value.Api}'"
                );
            }

            validatedConfigs++;
        }

        Assert.True(validatedConfigs > 0, "No config directories with Api.json were found.");
    }

    [Fact]
    public void ConfigLoader_NormalizesBooleanVariables_ToBool()
    {
        string root = ConfigApiTestSupport.FindRepositoryRoot();

        string[] directories =
        [
            Path.Combine(root, "App", "Config"),
            Path.Combine(root, "App", "Config.example"),
        ];

        int validatedConfigs = 0;

        foreach (string directory in directories)
        {
            string apiPath = Path.Combine(directory, "Api.json");

            if (!File.Exists(apiPath))
                continue;

            App.Models.Config.App loaded = ConfigApiTestSupport.LoadSplit(directory);

            foreach (Api api in loaded.Api.Values)
            {
                if (
                    api.Request.Query.Variables.TryGetValue(
                        "withGrokTranslatedBio",
                        out object? withGrokTranslatedBio
                    )
                )
                {
                    Assert.True(
                        withGrokTranslatedBio is bool,
                        $"{directory}: api '{api.Id}' expected bool for query.variables.withGrokTranslatedBio, got '{withGrokTranslatedBio?.GetType().Name ?? "null"}'"
                    );
                }

                if (
                    api.Request.Query.Variables.TryGetValue(
                        "includePromotedContent",
                        out object? includePromotedContent
                    )
                )
                {
                    Assert.True(
                        includePromotedContent is bool,
                        $"{directory}: api '{api.Id}' expected bool for query.variables.includePromotedContent, got '{includePromotedContent?.GetType().Name ?? "null"}'"
                    );
                }
            }

            validatedConfigs++;
        }

        Assert.True(validatedConfigs > 0, "No config directories with Api.json were found.");
    }
}
