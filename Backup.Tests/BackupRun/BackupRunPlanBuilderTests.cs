using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Models;
using Backup.Domain.BackupRun;

namespace Backup.Tests;

public class BackupRunPlanBuilderTests
{
    [Fact]
    public void Build_CreatesSourcesAndRunFlags()
    {
        BackupRunPlanInput input = new()
        {
            Users =
            [
                new BackupRunUserInput
                {
                    UserId = "user-1",
                    Api = new Dictionary<string, BackupRunApiPlan>
                    {
                        ["Api.SearchTimeline"] = CreateApi("SearchTimeline", true),
                        ["Api.UserMedia"] = CreateApi("UserMedia", true),
                    },
                },
                new BackupRunUserInput
                {
                    UserId = "user-2",
                    Api = new Dictionary<string, BackupRunApiPlan>
                    {
                        ["Api.SearchTimeline"] = CreateApi("SearchTimelineV2", true),
                    },
                },
            ],
            Fetch = new Dictionary<string, BackupRunFetchInput>
            {
                ["Api.SearchTimeline"] = new BackupRunFetchInput { Count = 50 },
                ["Api.UserMedia"] = new BackupRunFetchInput { Count = 25 },
            },
            IsBulkEnabled = true,
            IsMediaEnabled = true,
        };

        BackupRunPlanBuilder builder = new();
        BackupRunPlan plan = builder.Build(input);

        Assert.True(plan.IsBulkEnabled);
        Assert.True(plan.IsMediaEnabled);
        Assert.Equal(2, plan.Users.Count);
        Assert.True(plan.Users[0].RunRecovery);
        Assert.True(plan.Users[0].RunBulk);
        Assert.False(plan.Users[1].RunRecovery);
        Assert.False(plan.Users[1].RunBulk);
        Assert.Equal(2, plan.Users[0].Sources.Count);
        Assert.Single(plan.Users[1].Sources);
    }

    [Fact]
    public void Build_SkipsMissingOrDisabledApis()
    {
        BackupRunPlanInput input = new()
        {
            Users =
            [
                new BackupRunUserInput
                {
                    UserId = "user-1",
                    Api = new Dictionary<string, BackupRunApiPlan>
                    {
                        ["Api.Enabled"] = CreateApi("Enabled", true),
                        ["Api.Disabled"] = CreateApi("Disabled", false),
                    },
                },
            ],
            Fetch = new Dictionary<string, BackupRunFetchInput>
            {
                ["Api.Enabled"] = new BackupRunFetchInput { Count = 10 },
                ["Api.Disabled"] = new BackupRunFetchInput { Count = 10 },
                ["Api.Missing"] = new BackupRunFetchInput { Count = 10 },
            },
            IsBulkEnabled = false,
            IsMediaEnabled = true,
        };

        BackupRunPlanBuilder builder = new();
        BackupRunPlan plan = builder.Build(input);

        BackupRunUserPlan user = Assert.Single(plan.Users);
        BackupRunSourcePlan source = Assert.Single(user.Sources);
        Assert.Equal("Api.Enabled", source.SourceId);
    }

    private static BackupRunApiPlan CreateApi(string id, bool enabled) =>
        new()
        {
            Id = id,
            Enabled = enabled,
            Request = new BackupRunRequestPlan
            {
                Url = "https://x.com/graphql",
                Variables = new Dictionary<string, object?> { ["count"] = 20 },
                Features = new Dictionary<string, bool> { ["feature"] = true },
                FieldToggles = new Dictionary<string, bool> { ["field"] = false },
                Headers = new Dictionary<string, string> { ["authorization"] = "Bearer token" },
            },
        };
}
