using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;

namespace Backup.Tests;

public sealed class MediaCacheRecheckApplyPolicyServiceTests
{
    private readonly MediaCacheRecheckApplyPolicyService _sut = new(
        new MediaCacheEntryStateFactoryService()
    );

    [Fact]
    public void Apply_ReturnsInvalid_WhenDecisionIsInvalid()
    {
        MediaCacheRecheckApplyResult result = _sut.Apply(
            "/media/a.jpg",
            new MediaCacheRecheckResult { IsInvalid = true }
        );

        Assert.True(result.IsInvalid);
        Assert.False(result.ShouldRemove);
        Assert.Null(result.UpdatedEntryState);
    }

    [Fact]
    public void Apply_ReturnsRemove_WhenDecisionRequestsRemoval()
    {
        MediaCacheRecheckApplyResult result = _sut.Apply(
            "/media/a.jpg",
            new MediaCacheRecheckResult { ShouldRemove = true }
        );

        Assert.False(result.IsInvalid);
        Assert.True(result.ShouldRemove);
        Assert.Null(result.UpdatedEntryState);
    }

    [Fact]
    public void Apply_ReturnsEmpty_WhenNoUpdateRequested()
    {
        MediaCacheRecheckApplyResult result = _sut.Apply(
            "/media/a.jpg",
            new MediaCacheRecheckResult { ShouldUpdate = false }
        );

        Assert.False(result.IsInvalid);
        Assert.False(result.ShouldRemove);
        Assert.Null(result.UpdatedEntryState);
    }

    [Fact]
    public void Apply_CreatesUpdatedEntry_WhenUpdateRequested()
    {
        MediaCacheRecheckApplyResult result = _sut.Apply(
            "/media/a.jpg",
            new MediaCacheRecheckResult
            {
                ShouldUpdate = true,
                PartitionId = 3,
                StreamSizeBytes = 100,
                FileSizeBytes = 90,
            }
        );

        Assert.False(result.IsInvalid);
        Assert.False(result.ShouldRemove);
        Assert.NotNull(result.UpdatedEntryState);
        Assert.Equal("/media/a.jpg", result.UpdatedEntryState.Path);
        Assert.Equal(3, result.UpdatedEntryState.PartitionId);
        Assert.Equal(100, result.UpdatedEntryState.StreamSizeBytes);
        Assert.Equal(90, result.UpdatedEntryState.FileSizeBytes);
    }
}
