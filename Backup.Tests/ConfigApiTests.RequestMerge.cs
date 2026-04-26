using System.Globalization;
using Backup.App.Models.Config.Api;
using Backup.App.Models.Config.Request;

namespace Backup.Tests;

public partial class ConfigApiTests
{
    [Fact]
    public void RequestMerge_Build_ReturnsClonedRequest_WhenApiIsEnabled()
    {
        Request sourceRequest = new()
        {
            Url = "https://x.com/i/api/graphql/test/TweetDetail",
            Query = new()
            {
                Variables = new Dictionary<string, object?> { ["focalTweetId"] = "1" },
                Features = new Dictionary<string, bool> { ["featureA"] = true },
                FieldToggles = new Dictionary<string, bool> { ["toggleA"] = false },
            },
            Headers = new Dictionary<string, string>
            {
                ["Referer"] = "https://x.com/test/status/1",
            },
        };

        Dictionary<string, Api> api = new()
        {
            ["TweetDetail"] = new()
            {
                Id = "TweetDetail",
                Enabled = true,
                Request = sourceRequest,
            },
        };

        Request? request = RequestMerge.Build(api, "TweetDetail");

        Assert.NotNull(request);
        Assert.Equal(sourceRequest.Url, request!.Url);
        Assert.Equal("1", request.Query.Variables["focalTweetId"]?.ToString());

        request.Query.Variables["focalTweetId"] = "2";
        request.Headers["Referer"] = "https://x.com/changed";

        Assert.Equal("1", sourceRequest.Query.Variables["focalTweetId"]?.ToString());
        Assert.Equal("https://x.com/test/status/1", sourceRequest.Headers["Referer"]);
    }

    [Fact]
    public void RequestMerge_Build_ReturnsNull_WhenApiIsDisabledOrMissing()
    {
        Dictionary<string, Api> api = new()
        {
            ["TweetDetail"] = new()
            {
                Id = "TweetDetail",
                Enabled = false,
                Request = new()
                {
                    Url = "https://x.com/i/api/graphql/test/TweetDetail",
                    Query = new()
                    {
                        Variables = [],
                        Features = [],
                        FieldToggles = [],
                    },
                    Headers = [],
                },
            },
        };

        Assert.Null(RequestMerge.Build(api, "TweetDetail"));
        Assert.Null(RequestMerge.Build(api, "MissingApi"));
    }

    [Fact]
    public void RequestMerge_Build_CoercesStringBooleanVariables()
    {
        Request sourceRequest = new()
        {
            Url = "https://x.com/i/api/graphql/test/UserByScreenName",
            Query = new()
            {
                Variables = new Dictionary<string, object?>
                {
                    ["screen_name"] = "myugirlwholived",
                    ["withGrokTranslatedBio"] = "True",
                },
                Features = [],
                FieldToggles = [],
            },
            Headers = [],
        };

        Dictionary<string, Api> api = new()
        {
            ["UserByScreenName"] = new()
            {
                Id = "UserByScreenName",
                Enabled = true,
                Request = sourceRequest,
            },
        };

        Request request =
            RequestMerge.Build(api, "UserByScreenName")
            ?? throw new Exception("request should not be null");

        Assert.True(request.Query.Variables["withGrokTranslatedBio"] is bool);
        Assert.Equal(true, request.Query.Variables["withGrokTranslatedBio"]);
        Assert.Equal("True", sourceRequest.Query.Variables["withGrokTranslatedBio"]);
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

            Request sourceRequest = new()
            {
                Url = "https://x.com/i/api/graphql/test/UserByScreenName",
                Query = new()
                {
                    Variables = new Dictionary<string, object?>
                    {
                        ["screen_name"] = "myugirlwholived",
                        ["ratio"] = "1.25",
                    },
                    Features = [],
                    FieldToggles = [],
                },
                Headers = [],
            };

            Dictionary<string, Api> api = new()
            {
                ["UserByScreenName"] = new()
                {
                    Id = "UserByScreenName",
                    Enabled = true,
                    Request = sourceRequest,
                },
            };

            Request request =
                RequestMerge.Build(api, "UserByScreenName")
                ?? throw new Exception("request should not be null");

            Assert.True(request.Query.Variables["ratio"] is double);
            Assert.Equal(1.25d, (double)request.Query.Variables["ratio"]!, 10);
            Assert.Equal("1.25", sourceRequest.Query.Variables["ratio"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
