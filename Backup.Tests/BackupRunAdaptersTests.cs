using Backup.Domain.BackupRun;
using Backup.Infrastructure.BackupRun.Adapters;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.Tests;

public class BackupRunAdaptersTests
{
    [Fact]
    public async Task PostSourceRunner_MapsPlanToApiContext_AndCallsAllServices()
    {
        FakePostService serviceA = new();
        FakePostService serviceB = new();

        PostSourceRunnerAdapter runner = new(
            [serviceA, serviceB],
            NullLogger<PostSourceRunnerAdapter>.Instance
        );

        BackupRunSourcePlan source = new()
        {
            SourceId = "Api.SearchTimeline",
            ApiId = "SearchTimeline",
            Count = 55,
            Request = new BackupRunRequestPlan
            {
                Url = "https://x.com/graphql",
                Variables = new Dictionary<string, object?> { ["count"] = 20, ["cursor"] = "A1" },
                Features = new Dictionary<string, bool> { ["featureA"] = true },
                FieldToggles = new Dictionary<string, bool> { ["fieldA"] = false },
                Headers = new Dictionary<string, string> { ["authorization"] = "Bearer token" },
            },
        };

        await runner.Run("user-123", source);

        ApiContext context = Assert.Single(serviceA.DownloadCalls);
        Assert.Single(serviceB.DownloadCalls);
        Assert.Equal("SearchTimeline", context.Id);
        Assert.Equal("user-123", context.UserId);
        Assert.Equal(55, context.Count);
        Assert.Equal("https://x.com/graphql", context.Request.Url);
        Assert.Equal(20, Convert.ToInt32(context.Request.Query.Variables["count"]));
        Assert.Equal("A1", context.Request.Query.Variables["cursor"]?.ToString());
        Assert.True(context.Request.Query.Features["featureA"]);
        Assert.False(context.Request.Query.FieldToggles["fieldA"]);
        Assert.Equal("Bearer token", context.Request.Headers["authorization"]);
    }

    [Fact]
    public async Task PostRecoveryRunner_MapsPlanToUsersContext_AndCallsAllServices()
    {
        FakePostService serviceA = new();
        FakePostService serviceB = new();

        PostRecoveryRunnerAdapter runner = new(
            [serviceA, serviceB],
            NullLogger<PostRecoveryRunnerAdapter>.Instance
        );

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

        await runner.Run(user);

        UsersContext context = Assert.Single(serviceA.RecoverCalls);
        Assert.Single(serviceB.RecoverCalls);
        Assert.Equal("user-42", context.UserId);
        Assert.True(context.Api.TryGetValue("TweetDetail", out ApiConfig? api));
        Assert.NotNull(api);
        Assert.Equal("TweetDetail", api!.Id);
        Assert.True(api.Enabled);
        Assert.Equal("https://x.com/graphql/tweet", api.Request.Url);
        Assert.Equal("100", api.Request.Query.Variables["focalTweetId"]?.ToString());
        Assert.True(api.Request.Query.Features["featureX"]);
        Assert.False(api.Request.Query.FieldToggles["fieldX"]);
        Assert.Equal("csrf", api.Request.Headers["x-csrf-token"]);
    }

    private sealed class FakePostService : IPostService
    {
        public List<ApiContext> DownloadCalls { get; } = [];
        public List<UsersContext> RecoverCalls { get; } = [];

        public Task Recover(UsersContext context)
        {
            RecoverCalls.Add(context);
            return Task.CompletedTask;
        }

        public Task Download(ApiContext context)
        {
            DownloadCalls.Add(context);
            return Task.CompletedTask;
        }
    }
}
