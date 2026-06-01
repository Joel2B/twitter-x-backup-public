using Backup.Application.Posts;
using Backup.Application.Posts.Models;

namespace Backup.Tests;

public sealed class PostSnapshotVerificationPlanningServiceTests
{
    [Fact]
    public void Plan_Returns_NoComparison_When_History_Is_Empty()
    {
        PostSnapshotVerificationPlanningService sut = new(new PostHistoryLatestSelectionService());

        PostSnapshotVerificationPlan plan = sut.Plan("posts.json", []);

        Assert.False(plan.ShouldCompareWithHistory);
        Assert.Equal(string.Empty, plan.HistoryFilePath);
    }

    [Fact]
    public void Plan_Uses_Latest_History_Directory()
    {
        PostSnapshotVerificationPlanningService sut = new(new PostHistoryLatestSelectionService());

        List<PostHistoryPath> history =
        [
            new(@"c:\data\history\2026.01.01-00.00.00", new DateTime(2026, 1, 1)),
            new(@"c:\data\history\2026.01.03-00.00.00", new DateTime(2026, 1, 3)),
            new(@"c:\data\history\2026.01.02-00.00.00", new DateTime(2026, 1, 2)),
        ];

        PostSnapshotVerificationPlan plan = sut.Plan("posts.json", history);

        Assert.True(plan.ShouldCompareWithHistory);
        Assert.Equal("2026.01.03-00.00.00", plan.HistoryDirectoryName);
        Assert.EndsWith(@"2026.01.03-00.00.00\posts.json", plan.HistoryFilePath);
    }
}
