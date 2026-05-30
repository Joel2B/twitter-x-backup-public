using Backup.Domain.BackupRun;
using Backup.Infrastructure.BackupRun.Adapters;
using Backup.Infrastructure.Models.Config.Request;

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

}
