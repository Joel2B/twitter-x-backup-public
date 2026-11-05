using Backup.Application.Media.Maintenance;

namespace Backup.Tests.Media.Cache;

public sealed class MediaCachePartitionSelectionServiceTests
{
    private readonly MediaCachePartitionSelectionService _service = new();

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    public void New_path_uses_inclusive_heavy_threshold(long size, bool expectedHeavy)
    {
        var result = _service.Select(size, heavyThresholdBytes: 10, cachedPartitionId: null);

        Assert.Equal(expectedHeavy, result.UseHeavyPartition);
        Assert.Null(result.PreferredPartitionId);
    }

    [Theory]
    [InlineData(7, 100)]
    [InlineData(4, 1)]
    public void Existing_path_keeps_recorded_partition(int partitionId, long size)
    {
        var result = _service.Select(size, heavyThresholdBytes: 10, partitionId);

        Assert.False(result.UseHeavyPartition);
        Assert.Equal(partitionId, result.PreferredPartitionId);
    }
}
