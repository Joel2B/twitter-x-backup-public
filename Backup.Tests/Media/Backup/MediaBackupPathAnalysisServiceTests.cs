using Backup.Application.Media.Backup;

namespace Backup.Tests;

public class MediaBackupPathAnalysisServiceTests
{
    [Fact]
    public void Plan_FindsDuplicates_AndBuildsCleanup()
    {
        MediaBackupDuplicateCheckPlanningService sut = new(
            new MediaBackupDuplicateCleanupService(),
            new MediaBackupStorageConsistencyDecisionService(
                new MediaBackupChunkReconciliationService()
            )
        );
        string[] paths = ["a.jpg", "b.jpg", "a.jpg", "c.jpg", "b.jpg"];

        var plan = sut.Plan(paths, paths);

        Assert.Equal(2, plan.MemoryDuplicatePathCount);
        Assert.Equal(4, plan.MemoryDuplicateEntryCount);
        Assert.Equal(2, plan.StorageDuplicatePathCount);
        Assert.NotNull(plan.StorageCleanupPlan);
        Assert.Equal(2, plan.StorageCleanupPlan!.Operations.Count);
    }

    [Fact]
    public void Plan_DetectsMissingAndExtras()
    {
        MediaBackupDuplicateCheckPlanningService sut = new(
            new MediaBackupDuplicateCleanupService(),
            new MediaBackupStorageConsistencyDecisionService(
                new MediaBackupChunkReconciliationService()
            )
        );
        string[] expected = ["a.jpg", "b.jpg", "d.jpg"];
        string[] actual = ["a.jpg", "c.jpg", "b.jpg"];

        var plan = sut.Plan(expected, actual);

        Assert.Equal(1, plan.ConsistencyDecision.MissingCount);
        Assert.Single(plan.ConsistencyDecision.ExtraPaths);
        Assert.Equal("c.jpg", plan.ConsistencyDecision.ExtraPaths[0]);
    }
}
