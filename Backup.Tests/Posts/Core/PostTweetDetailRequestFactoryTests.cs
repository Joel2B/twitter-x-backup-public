using Backup.Application.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;
using Backup.Infrastructure.Posts.Adapters;

namespace Backup.Tests;

public class PostTweetDetailRequestFactoryTests
{
    [Fact]
    public void Build_WhenTweetDetailExistsAndEnabled_ReturnsClonedRequest()
    {
        Request request = CreateRequest("https://x.com/graphql/tweet-detail");
        UsersContext context = new()
        {
            UserId = "user-1",
            Api = new Dictionary<string, ApiConfig>
            {
                ["TweetDetail"] = new()
                {
                    Id = "TweetDetail",
                    Enabled = true,
                    Request = request,
                },
            },
        };

        PostTweetDetailRequestFactory sut = new(new ApiRequestBuildService());

        Request? built = sut.Build(context);

        Assert.NotNull(built);
        Assert.NotSame(request, built);
        Assert.Equal("https://x.com/graphql/tweet-detail", built!.Url);

        built.Query.Variables["focalTweetId"] = "changed";
        Assert.Equal("100", request.Query.Variables["focalTweetId"]?.ToString());
    }

    [Fact]
    public void Build_WhenTweetDetailMissing_ReturnsNull()
    {
        UsersContext context = new()
        {
            UserId = "user-1",
            Api = new Dictionary<string, ApiConfig>(),
        };

        PostTweetDetailRequestFactory sut = new(new ApiRequestBuildService());

        Request? built = sut.Build(context);

        Assert.Null(built);
    }

    [Fact]
    public void Build_WhenTweetDetailDisabled_ReturnsNull()
    {
        UsersContext context = new()
        {
            UserId = "user-1",
            Api = new Dictionary<string, ApiConfig>
            {
                ["TweetDetail"] = new()
                {
                    Id = "TweetDetail",
                    Enabled = false,
                    Request = CreateRequest("https://x.com/graphql/tweet-detail"),
                },
            },
        };

        PostTweetDetailRequestFactory sut = new(new ApiRequestBuildService());

        Request? built = sut.Build(context);

        Assert.Null(built);
    }

    private static Request CreateRequest(string url) =>
        new()
        {
            Url = url,
            Query = new Query
            {
                Variables = new Dictionary<string, object?> { ["focalTweetId"] = "100" },
                Features = new Dictionary<string, bool> { ["featureA"] = true },
                FieldToggles = new Dictionary<string, bool> { ["fieldA"] = false },
            },
            Headers = new Dictionary<string, string> { ["authorization"] = "Bearer token" },
        };
}
