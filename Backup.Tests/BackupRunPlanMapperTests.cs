using Backup.Application.BackupRun.Models;
using Backup.Infrastructure.BackupRun.Adapters;
using Backup.Infrastructure.Models.Config.ApiRequest;

namespace Backup.Tests;

public class BackupRunPlanMapperTests
{
    [Fact]
    public void ToPlanRequest_MapsAndClonesRequest()
    {
        Request request = new()
        {
            Url = "https://x.com/graphql/search",
            Query = new Query
            {
                Variables = new Dictionary<string, object?> { ["count"] = 20, ["cursor"] = "C1" },
                Features = new Dictionary<string, bool> { ["featureA"] = true },
                FieldToggles = new Dictionary<string, bool> { ["fieldA"] = false },
            },
            Headers = new Dictionary<string, string> { ["authorization"] = "Bearer token" },
        };

        BackupRunRequestPlan plan = BackupRunPlanMapper.ToPlanRequest(request);

        Assert.Equal(request.Url, plan.Url);
        Assert.Equal(20, Convert.ToInt32(plan.Variables["count"]));
        Assert.Equal("C1", plan.Variables["cursor"]?.ToString());
        Assert.True(plan.Features["featureA"]);
        Assert.False(plan.FieldToggles["fieldA"]);
        Assert.Equal("Bearer token", plan.Headers["authorization"]);

        Assert.NotSame(request.Query.Variables, plan.Variables);
        Assert.NotSame(request.Query.Features, plan.Features);
        Assert.NotSame(request.Query.FieldToggles, plan.FieldToggles);
        Assert.NotSame(request.Headers, plan.Headers);
    }

    [Fact]
    public void ToRequest_MapsAndClonesPlan()
    {
        BackupRunRequestPlan plan = new()
        {
            Url = "https://x.com/graphql/media",
            Variables = new Dictionary<string, object?> { ["count"] = 50, ["cursor"] = "NEXT" },
            Features = new Dictionary<string, bool> { ["featureB"] = true },
            FieldToggles = new Dictionary<string, bool> { ["fieldB"] = false },
            Headers = new Dictionary<string, string> { ["x-csrf-token"] = "csrf" },
        };

        Request request = BackupRunPlanMapper.ToRequest(plan);

        Assert.Equal(plan.Url, request.Url);
        Assert.Equal(50, Convert.ToInt32(request.Query.Variables["count"]));
        Assert.Equal("NEXT", request.Query.Variables["cursor"]?.ToString());
        Assert.True(request.Query.Features["featureB"]);
        Assert.False(request.Query.FieldToggles["fieldB"]);
        Assert.Equal("csrf", request.Headers["x-csrf-token"]);

        Assert.NotSame(plan.Variables, request.Query.Variables);
        Assert.NotSame(plan.Features, request.Query.Features);
        Assert.NotSame(plan.FieldToggles, request.Query.FieldToggles);
        Assert.NotSame(plan.Headers, request.Headers);
    }

    [Fact]
    public void ToApiContext_MapsSourcePlanToApiContext()
    {
        BackupRunSourcePlan source = new()
        {
            SourceId = "Api.SearchTimeline",
            ApiId = "SearchTimeline",
            Count = 100,
            Request = new BackupRunRequestPlan
            {
                Url = "https://x.com/graphql/search",
                Variables = new Dictionary<string, object?> { ["count"] = 40 },
                Features = new Dictionary<string, bool> { ["featureC"] = true },
                FieldToggles = new Dictionary<string, bool> { ["fieldC"] = false },
                Headers = new Dictionary<string, string> { ["authorization"] = "Bearer abc" },
            },
        };

        var context = BackupRunPlanMapper.ToApiContext("user-abc", source);

        Assert.Equal("SearchTimeline", context.Id);
        Assert.Equal("user-abc", context.UserId);
        Assert.Equal(100, context.Count);
        Assert.Equal("https://x.com/graphql/search", context.Request.Url);
        Assert.Equal(40, Convert.ToInt32(context.Request.Query.Variables["count"]));
        Assert.True(context.Request.Query.Features["featureC"]);
        Assert.False(context.Request.Query.FieldToggles["fieldC"]);
    }

    [Fact]
    public void ToUsersContext_MapsUserPlanToUsersContext()
    {
        BackupRunUserPlan user = new()
        {
            UserId = "user-999",
            Api = new Dictionary<string, BackupRunApiPlan>
            {
                ["TweetDetail"] = new BackupRunApiPlan
                {
                    Id = "TweetDetail",
                    Enabled = true,
                    Request = new BackupRunRequestPlan
                    {
                        Url = "https://x.com/graphql/tweet",
                        Variables = new Dictionary<string, object?> { ["focalTweetId"] = "123" },
                        Features = new Dictionary<string, bool> { ["featureD"] = true },
                        FieldToggles = new Dictionary<string, bool> { ["fieldD"] = false },
                        Headers = new Dictionary<string, string> { ["x-csrf-token"] = "token" },
                    },
                },
            },
            Sources = [],
            RunRecovery = true,
            RunBulk = false,
        };

        var context = BackupRunPlanMapper.ToUsersContext(user);

        Assert.Equal("user-999", context.UserId);
        Assert.True(context.Api.TryGetValue("TweetDetail", out var api));
        Assert.NotNull(api);
        Assert.Equal("TweetDetail", api!.Id);
        Assert.True(api.Enabled);
        Assert.Equal("https://x.com/graphql/tweet", api.Request.Url);
        Assert.Equal("123", api.Request.Query.Variables["focalTweetId"]?.ToString());
        Assert.True(api.Request.Query.Features["featureD"]);
        Assert.False(api.Request.Query.FieldToggles["fieldD"]);
        Assert.Equal("token", api.Request.Headers["x-csrf-token"]);
    }
}
