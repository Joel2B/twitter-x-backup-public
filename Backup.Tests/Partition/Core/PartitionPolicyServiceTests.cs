using Backup.Application.Partition;
using Backup.Application.Partition.Models;

namespace Backup.Tests;

public class PartitionPolicyServiceTests
{
    [Fact]
    public void ResolvePartitionId_SizeZero_ReturnsPrimary()
    {
        PartitionPolicyService sut = new();

        int id = sut.ResolvePartitionId(CreatePartitions(), requestedId: null, size: 0);

        Assert.Equal(1, id);
    }

    [Fact]
    public void ResolvePartitionId_SizePositive_UsesExtensionWhenPrimaryIsFull()
    {
        PartitionPolicyService sut = new();
        List<PartitionState> partitions =
        [
            new()
            {
                Id = 1,
                Type = "primary",
                Enabled = true,
                Size = 1,
                UsableSpace = 100,
                CurrentSize = 1_000_000_000,
            },
            new()
            {
                Id = 2,
                Type = "extension",
                Enabled = true,
                Size = 2,
                UsableSpace = 100,
                CurrentSize = 0,
            },
        ];

        int id = sut.ResolvePartitionId(partitions, requestedId: null, size: 100);

        Assert.Equal(2, id);
    }

    [Fact]
    public void IsCachePartition_ReturnsTrue_ForTypeOrTag()
    {
        PartitionPolicyService sut = new();

        Assert.True(
            sut.IsCachePartition(
                new()
                {
                    Id = 1,
                    Type = "cache",
                    Enabled = true,
                }
            )
        );
        Assert.True(
            sut.IsCachePartition(
                new()
                {
                    Id = 2,
                    Type = "extension",
                    Tags = ["cache"],
                    Enabled = true,
                }
            )
        );
        Assert.False(
            sut.IsCachePartition(
                new()
                {
                    Id = 3,
                    Type = "primary",
                    Enabled = true,
                }
            )
        );
    }

    private static List<PartitionState> CreatePartitions() =>
        [
            new()
            {
                Id = 1,
                Type = "primary",
                Enabled = true,
                Size = 2,
                UsableSpace = 100,
                CurrentSize = 0,
            },
            new()
            {
                Id = 2,
                Type = "extension",
                Enabled = true,
                Size = 2,
                UsableSpace = 100,
                CurrentSize = 0,
            },
        ];
}
