using Backup.Application.Posts;

namespace Backup.Tests;

public sealed class PostDataReplicationPlanningServiceTests
{
    [Fact]
    public void Plan_Pairs_Source_And_Target_By_Index()
    {
        PostDataReplicationPlanningService sut = new();

        var plan = sut.Plan(
            ["a/posts.json", "a/profiles.json"],
            ["b/posts.json", "b/profiles.json"]
        );

        Assert.Equal(2, plan.Count);
        Assert.Equal("a/posts.json", plan[0].SourcePath);
        Assert.Equal("b/posts.json", plan[0].TargetPath);
        Assert.Equal("a/profiles.json", plan[1].SourcePath);
        Assert.Equal("b/profiles.json", plan[1].TargetPath);
    }

    [Fact]
    public void Plan_Throws_When_Counts_Do_Not_Match()
    {
        PostDataReplicationPlanningService sut = new();

        Assert.Throws<InvalidOperationException>(() => sut.Plan(["a"], ["b", "c"]));
    }
}
