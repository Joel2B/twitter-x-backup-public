using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;

namespace Backup.Tests;

public sealed class MediaCacheWritePolicyServiceTests
{
    private readonly MediaCacheEntryPathPolicyService _pathPolicy = new();
    private readonly MediaCacheWritePolicyService _sut;

    public MediaCacheWritePolicyServiceTests()
    {
        _sut = new MediaCacheWritePolicyService(_pathPolicy, new MediaCacheEntryStateFactoryService());
    }

    [Fact]
    public void BuildWritePlan_NormalizesPathAndCreatesEntryState()
    {
        MediaCacheWritePlan plan = _sut.BuildWritePlan("a/b/c.jpg", 7, 1234);

        string expectedPath = _pathPolicy.NormalizeForCacheKey("a/b/c.jpg");
        Assert.Equal(expectedPath, plan.CacheKey);
        Assert.Equal(expectedPath, plan.EntryState.Path);
        Assert.Equal(7, plan.EntryState.PartitionId);
        Assert.Equal(1234, plan.EntryState.StreamSizeBytes);
        Assert.Null(plan.EntryState.FileSizeBytes);
    }

    [Fact]
    public void HasConflict_ReturnsTrue_WhenExistingAndIncomingDiffer()
    {
        MediaCacheWritePlan plan = _sut.BuildWritePlan("a/b/c.jpg", 7, 1234);

        bool hasConflict = _sut.HasConflict(99, plan);

        Assert.True(hasConflict);
    }

    [Fact]
    public void HasConflict_ReturnsFalse_WhenExistingIsNullOrEqual()
    {
        MediaCacheWritePlan plan = _sut.BuildWritePlan("a/b/c.jpg", 7, 1234);

        Assert.False(_sut.HasConflict(null, plan));
        Assert.False(_sut.HasConflict(1234, plan));
    }
}
