using Backup.Application.BackupRun.Models;
using Backup.Infrastructure.BackupRun.Adapters;

namespace Backup.Tests;

public class BackupRunExecutionContextMapperTests
{
    [Fact]
    public void ToApiContext_MapsExecution()
    {
        BackupRunSourceExecution execution = new()
        {
            UserId = "user-1",
            SourceId = "Api.SearchTimeline",
            ApiId = "SearchTimeline",
            Count = 30,
            Request = new BackupRunRequestExecution
            {
                Url = "https://x.com/graphql/search",
                Variables = new Dictionary<string, object?> { ["count"] = 20, ["cursor"] = "A1" },
                Features = new Dictionary<string, bool> { ["featureA"] = true },
                FieldToggles = new Dictionary<string, bool> { ["fieldA"] = false },
                Headers = new Dictionary<string, string> { ["authorization"] = "Bearer token" },
            },
        };

        BackupRunExecutionContextMapper mapper = new();
        var context = mapper.ToApiContext(execution);

        Assert.Equal("SearchTimeline", context.Id);
        Assert.Equal("user-1", context.UserId);
        Assert.Equal(30, context.Count);
        Assert.Equal("https://x.com/graphql/search", context.Request.Url);
        Assert.Equal(20, Convert.ToInt32(context.Request.Query.Variables["count"]));
        Assert.Equal("A1", context.Request.Query.Variables["cursor"]?.ToString());
        Assert.True(context.Request.Query.Features["featureA"]);
        Assert.False(context.Request.Query.FieldToggles["fieldA"]);
        Assert.Equal("Bearer token", context.Request.Headers["authorization"]);
    }

    [Fact]
    public void ToUsersContext_MapsExecution()
    {
        BackupRunRecoveryExecution execution = new()
        {
            UserId = "user-2",
            Api = new Dictionary<string, BackupRunApiExecution>
            {
                ["TweetDetail"] = new BackupRunApiExecution
                {
                    Id = "TweetDetail",
                    Enabled = true,
                    Request = new BackupRunRequestExecution
                    {
                        Url = "https://x.com/graphql/tweet",
                        Variables = new Dictionary<string, object?> { ["focalTweetId"] = "100" },
                        Features = new Dictionary<string, bool> { ["featureX"] = true },
                        FieldToggles = new Dictionary<string, bool> { ["fieldX"] = false },
                        Headers = new Dictionary<string, string> { ["x-csrf-token"] = "csrf" },
                    },
                },
            },
        };

        BackupRunExecutionContextMapper mapper = new();
        var context = mapper.ToUsersContext(execution);

        Assert.Equal("user-2", context.UserId);
        Assert.True(context.Api.TryGetValue("TweetDetail", out var api));
        Assert.NotNull(api);
        Assert.Equal("TweetDetail", api!.Id);
        Assert.True(api.Enabled);
        Assert.Equal("https://x.com/graphql/tweet", api.Request.Url);
        Assert.Equal("100", api.Request.Query.Variables["focalTweetId"]?.ToString());
        Assert.True(api.Request.Query.Features["featureX"]);
        Assert.False(api.Request.Query.FieldToggles["fieldX"]);
        Assert.Equal("csrf", api.Request.Headers["x-csrf-token"]);
    }
}
