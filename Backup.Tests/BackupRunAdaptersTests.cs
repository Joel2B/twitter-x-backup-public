using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.BackupRun.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.Tests;

public class BackupRunAdaptersTests
{
    [Fact]
    public async Task PostSourceRunner_PassesExecutionToAllServices()
    {
        FakePostSourceExecutionService serviceA = new();
        FakePostSourceExecutionService serviceB = new();

        PostSourceRunnerAdapter runner = new(
            new BackupRunStepExecutor(),
            [serviceA, serviceB],
            NullLogger<PostSourceRunnerAdapter>.Instance
        );

        BackupRunSourceExecution execution = new()
        {
            UserId = "user-123",
            SourceId = "Api.SearchTimeline",
            ApiId = "SearchTimeline",
            Count = 55,
            Request = new BackupRunRequestExecution
            {
                Url = "https://x.com/graphql",
                Variables = new Dictionary<string, object?> { ["count"] = 20, ["cursor"] = "A1" },
                Features = new Dictionary<string, bool> { ["featureA"] = true },
                FieldToggles = new Dictionary<string, bool> { ["fieldA"] = false },
                Headers = new Dictionary<string, string> { ["authorization"] = "Bearer token" },
            },
        };

        await runner.Run(execution);

        BackupRunSourceExecution context = Assert.Single(serviceA.DownloadCalls);
        Assert.Single(serviceB.DownloadCalls);
        Assert.Equal("SearchTimeline", context.ApiId);
        Assert.Equal("user-123", context.UserId);
        Assert.Equal(55, context.Count);
        Assert.Equal("https://x.com/graphql", context.Request.Url);
        Assert.Equal(20, Convert.ToInt32(context.Request.Variables["count"]));
        Assert.Equal("A1", context.Request.Variables["cursor"]?.ToString());
        Assert.True(context.Request.Features["featureA"]);
        Assert.False(context.Request.FieldToggles["fieldA"]);
        Assert.Equal("Bearer token", context.Request.Headers["authorization"]);
    }

    [Fact]
    public async Task PostRecoveryRunner_PassesExecutionToAllServices()
    {
        FakePostRecoveryExecutionService serviceA = new();
        FakePostRecoveryExecutionService serviceB = new();

        PostRecoveryRunnerAdapter runner = new(
            new BackupRunStepExecutor(),
            [serviceA, serviceB],
            NullLogger<PostRecoveryRunnerAdapter>.Instance
        );

        BackupRunRecoveryExecution execution = new()
        {
            UserId = "user-42",
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

        await runner.Run(execution);

        BackupRunRecoveryExecution context = Assert.Single(serviceA.RecoverCalls);
        Assert.Single(serviceB.RecoverCalls);
        Assert.Equal("user-42", context.UserId);
        Assert.True(context.Api.TryGetValue("TweetDetail", out BackupRunApiExecution? api));
        Assert.NotNull(api);
        Assert.Equal("TweetDetail", api!.Id);
        Assert.True(api.Enabled);
        Assert.Equal("https://x.com/graphql/tweet", api.Request.Url);
        Assert.Equal("100", api.Request.Variables["focalTweetId"]?.ToString());
        Assert.True(api.Request.Features["featureX"]);
        Assert.False(api.Request.FieldToggles["fieldX"]);
        Assert.Equal("csrf", api.Request.Headers["x-csrf-token"]);
    }

    private sealed class FakePostSourceExecutionService : IPostSourceExecutionService
    {
        public List<BackupRunSourceExecution> DownloadCalls { get; } = [];

        public Task Download(BackupRunSourceExecution execution)
        {
            DownloadCalls.Add(execution);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePostRecoveryExecutionService : IPostRecoveryExecutionService
    {
        public List<BackupRunRecoveryExecution> RecoverCalls { get; } = [];

        public Task Recover(BackupRunRecoveryExecution execution)
        {
            RecoverCalls.Add(execution);
            return Task.CompletedTask;
        }
    }
}
