using System.Globalization;
using Backup.Application.Config;
using Backup.Application.Config.Models;

namespace Backup.Tests;

public partial class ConfigApiTests
{
    [Fact]
    public void RequestMerge_Build_ReturnsClonedRequest_WhenApiIsEnabled()
    {
        ApiRequestBuildSource source = new()
        {
            Enabled = true,
            Url = "https://x.com/i/api/graphql/test/TweetDetail",
            Variables = new Dictionary<string, object?> { ["focalTweetId"] = "1" },
            Features = new Dictionary<string, bool> { ["featureA"] = true },
            FieldToggles = new Dictionary<string, bool> { ["toggleA"] = false },
            Headers = new Dictionary<string, string> { ["Referer"] = "https://x.com/test/status/1" },
        };
        IApiRequestBuildService service = new ApiRequestBuildService();
        Dictionary<string, ApiRequestBuildSource> api = new() { ["TweetDetail"] = source };

        ApiRequestBuildResult? request = service.Build(api, "TweetDetail");

        Assert.NotNull(request);
        Assert.Equal(source.Url, request!.Url);
        Assert.Equal("1", request.Variables["focalTweetId"]?.ToString());

        request.Variables["focalTweetId"] = "2";
        request.Headers["Referer"] = "https://x.com/changed";

        Assert.Equal("1", source.Variables["focalTweetId"]?.ToString());
        Assert.Equal("https://x.com/test/status/1", source.Headers!["Referer"]);
    }

    [Fact]
    public void RequestMerge_Build_ReturnsNull_WhenApiIsDisabledOrMissing()
    {
        IApiRequestBuildService service = new ApiRequestBuildService();
        Dictionary<string, ApiRequestBuildSource> api = new()
        {
            ["TweetDetail"] = new ApiRequestBuildSource
            {
                Enabled = false,
                Url = "https://x.com/i/api/graphql/test/TweetDetail",
                Variables = [],
                Features = [],
                FieldToggles = [],
                Headers = [],
            },
        };

        Assert.Null(service.Build(api, "TweetDetail"));
        Assert.Null(service.Build(api, "MissingApi"));
    }

    [Fact]
    public void RequestMerge_Build_CoercesStringBooleanVariables()
    {
        ApiRequestBuildSource source = new()
        {
            Enabled = true,
            Url = "https://x.com/i/api/graphql/test/UserByScreenName",
            Variables = new Dictionary<string, object?>
            {
                ["screen_name"] = "myugirlwholived",
                ["withGrokTranslatedBio"] = "True",
            },
            Features = [],
            FieldToggles = [],
            Headers = [],
        };
        IApiRequestBuildService service = new ApiRequestBuildService();
        Dictionary<string, ApiRequestBuildSource> api = new() { ["UserByScreenName"] = source };

        ApiRequestBuildResult request =
            service.Build(api, "UserByScreenName") ?? throw new Exception("request should not be null");

        Assert.True(request.Variables["withGrokTranslatedBio"] is bool);
        Assert.Equal(true, request.Variables["withGrokTranslatedBio"]);
        Assert.Equal("True", source.Variables["withGrokTranslatedBio"]);
    }

    [Fact]
    public void RequestMerge_Build_CoercesStringDoubleVariables_UsingInvariantCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
            CultureInfo.CurrentUICulture = new CultureInfo("es-ES");

            ApiRequestBuildSource source = new()
            {
                Enabled = true,
                Url = "https://x.com/i/api/graphql/test/UserByScreenName",
                Variables = new Dictionary<string, object?>
                {
                    ["screen_name"] = "myugirlwholived",
                    ["ratio"] = "1.25",
                },
                Features = [],
                FieldToggles = [],
                Headers = [],
            };
            IApiRequestBuildService service = new ApiRequestBuildService();
            Dictionary<string, ApiRequestBuildSource> api = new() { ["UserByScreenName"] = source };

            ApiRequestBuildResult request =
                service.Build(api, "UserByScreenName")
                ?? throw new Exception("request should not be null");

            Assert.True(request.Variables["ratio"] is double);
            Assert.Equal(1.25d, (double)request.Variables["ratio"]!, 10);
            Assert.Equal("1.25", source.Variables["ratio"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
