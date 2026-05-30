using Backup.Application.BackupRun;
using Backup.Domain.BackupRun;

namespace Backup.Tests;

public class BackupRunExecutionMapperTests
{
    [Fact]
    public void MapSource_MapsAndClonesValues()
    {
        BackupRunExecutionMapper mapper = new();
        BackupRunSourcePlan source = new()
        {
            SourceId = "Api.SearchTimeline",
            ApiId = "SearchTimeline",
            Count = 25,
            Request = new BackupRunRequestPlan
            {
                Url = "https://x.com/graphql/search",
                Variables = new Dictionary<string, object?> { ["count"] = 20 },
                Features = new Dictionary<string, bool> { ["featureA"] = true },
                FieldToggles = new Dictionary<string, bool> { ["fieldA"] = false },
                Headers = new Dictionary<string, string> { ["authorization"] = "Bearer token" },
            },
        };

        var execution = mapper.MapSource("user-1", source);

        Assert.Equal("user-1", execution.UserId);
        Assert.Equal("Api.SearchTimeline", execution.SourceId);
        Assert.Equal("SearchTimeline", execution.ApiId);
        Assert.Equal(25, execution.Count);
        Assert.Equal("https://x.com/graphql/search", execution.Request.Url);
        Assert.Equal(20, Convert.ToInt32(execution.Request.Variables["count"]));
        Assert.True(execution.Request.Features["featureA"]);
        Assert.False(execution.Request.FieldToggles["fieldA"]);
        Assert.Equal("Bearer token", execution.Request.Headers["authorization"]);
        Assert.NotSame(source.Request.Variables, execution.Request.Variables);
    }

    [Fact]
    public void MapRecovery_MapsAndClonesValues()
    {
        BackupRunExecutionMapper mapper = new();
        BackupRunUserPlan user = new()
        {
            UserId = "user-42",
            Api = new Dictionary<string, BackupRunApiPlan>
            {
                ["TweetDetail"] = new BackupRunApiPlan
                {
                    Id = "TweetDetail",
                    Enabled = true,
                    Request = new BackupRunRequestPlan
                    {
                        Url = "https://x.com/graphql/tweet",
                        Variables = new Dictionary<string, object?> { ["focalTweetId"] = "100" },
                        Features = new Dictionary<string, bool> { ["featureX"] = true },
                        FieldToggles = new Dictionary<string, bool> { ["fieldX"] = false },
                        Headers = new Dictionary<string, string> { ["x-csrf-token"] = "csrf" },
                    },
                },
            },
            Sources = [],
            RunRecovery = true,
            RunBulk = false,
        };

        var execution = mapper.MapRecovery(user);

        Assert.Equal("user-42", execution.UserId);
        Assert.True(execution.Api.TryGetValue("TweetDetail", out var api));
        Assert.NotNull(api);
        Assert.Equal("TweetDetail", api!.Id);
        Assert.True(api.Enabled);
        Assert.Equal("https://x.com/graphql/tweet", api.Request.Url);
        Assert.Equal("100", api.Request.Variables["focalTweetId"]?.ToString());
        Assert.True(api.Request.Features["featureX"]);
        Assert.False(api.Request.FieldToggles["fieldX"]);
        Assert.Equal("csrf", api.Request.Headers["x-csrf-token"]);
        Assert.NotSame(user.Api["TweetDetail"].Request.Headers, api.Request.Headers);
    }
}
