using Backup.Application.Partition;
using Backup.Application.Partition.Models;

namespace Backup.Tests;

public sealed class PartitionResolutionServiceTests
{
    private readonly PartitionResolutionService _sut = new(
        new PartitionStateProjectionService(),
        new PartitionPolicyService()
    );

    [Fact]
    public void SelectEnabledIds_FiltersByEnabledAndRequestedIds()
    {
        IReadOnlyCollection<int> ids = _sut.SelectEnabledIds(CreateSources(), selectedIds: [1, 3]);

        Assert.Equal([1], ids);
    }

    [Fact]
    public void ResolvePartitionId_SelectsRequestedPartitionWhenValid()
    {
        int id = _sut.ResolvePartitionId(CreateSources(), requestedId: 2, size: 0);

        Assert.Equal(2, id);
    }

    [Fact]
    public void SelectCacheIds_ReturnsTypeAndTagMatches()
    {
        IReadOnlyCollection<int> ids = _sut.SelectCacheIds(CreateSources());

        Assert.Equal([2, 3], ids.OrderBy(x => x).ToList());
    }

    [Fact]
    public void GetRequiredPartitionIdByType_ReturnsMatchingId()
    {
        int id = _sut.GetRequiredPartitionIdByType(CreateSources(), "primary");

        Assert.Equal(1, id);
    }

    private static IReadOnlyList<PartitionStateSource> CreateSources() =>
        [
            new()
            {
                Id = 1,
                Type = "primary",
                Tags = null,
                Size = 2,
                UsableSpace = 100,
                Enabled = true,
                CurrentSize = 0,
            },
            new()
            {
                Id = 2,
                Type = "cache",
                Tags = null,
                Size = 2,
                UsableSpace = 100,
                Enabled = true,
                CurrentSize = 0,
            },
            new()
            {
                Id = 3,
                Type = "extension",
                Tags = ["cache"],
                Size = 2,
                UsableSpace = 100,
                Enabled = false,
                CurrentSize = 0,
            },
        ];
}
