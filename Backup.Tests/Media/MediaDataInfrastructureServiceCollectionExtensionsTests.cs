using Backup.Application.Partition;
using Backup.Infrastructure.DependencyInjection.Features.Media;
using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Tests;

public class MediaDataInfrastructureServiceCollectionExtensionsTests
{
    private readonly PartitionResolutionService _partitionResolutionService = new(
        new PartitionStateProjectionService(),
        new PartitionPolicyService()
    );

    [Fact]
    public void ResolveCacheType_UsesJson_WhenMissing()
    {
        MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType type =
            MediaDataInfrastructureServiceCollectionExtensions.ResolveCacheType(null);

        Assert.Equal(MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType.Json, type);
    }

    [Fact]
    public void ResolveCacheType_UsesJson_WhenJson()
    {
        MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType type =
            MediaDataInfrastructureServiceCollectionExtensions.ResolveCacheType("json");

        Assert.Equal(MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType.Json, type);
    }

    [Fact]
    public void ResolveCacheType_UsesSqlite_WhenSqlite()
    {
        MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType type =
            MediaDataInfrastructureServiceCollectionExtensions.ResolveCacheType("sqlite");

        Assert.Equal(
            MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType.Sqlite,
            type
        );
    }

    [Fact]
    public void ResolveCacheType_UsesPostgres_WhenPostgres()
    {
        MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType type =
            MediaDataInfrastructureServiceCollectionExtensions.ResolveCacheType("postgres");

        Assert.Equal(
            MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType.Postgres,
            type
        );
    }

    [Fact]
    public void ResolveCacheType_Throws_WhenUnknown()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => MediaDataInfrastructureServiceCollectionExtensions.ResolveCacheType("invalid")
        );

        Assert.Contains("Allowed: json, sqlite, postgres.", ex.Message);
    }

    [Fact]
    public void ResolveEnabledCachePartitionIds_FiltersToConfiguredEnabledPartitions()
    {
        List<PartitionConfig> partitions =
        [
            CreatePartition(0, "primary"),
            CreatePartition(4, "heavy"),
            CreatePartition(7, "extension", ["cache"]),
            CreatePartition(8, "extension", ["cache"], enabled: false),
        ];

        IReadOnlyCollection<int> ids =
            MediaDataInfrastructureServiceCollectionExtensions.ResolveEnabledCachePartitionIds(
                _partitionResolutionService,
                partitions,
                [0, 7, 8]
            );

        Assert.Equal([0, 7], ids.OrderBy(id => id));
    }

    [Fact]
    public void ResolveCacheReplicaPartitionIds_ReturnsOnlyCacheReplicaPartitions()
    {
        List<PartitionConfig> partitions =
        [
            CreatePartition(0, "primary"),
            CreatePartition(4, "heavy"),
            CreatePartition(7, "extension", ["cache"]),
            CreatePartition(9, "extension"),
        ];

        IReadOnlyCollection<int> ids =
            MediaDataInfrastructureServiceCollectionExtensions.ResolveCacheReplicaPartitionIds(
                _partitionResolutionService,
                partitions
            );

        Assert.Equal([7], ids);
    }

    private static PartitionConfig CreatePartition(
        int id,
        string type,
        List<string>? tags = null,
        bool enabled = true
    ) =>
        new()
        {
            Id = id,
            Type = type,
            Tags = tags,
            Size = 100,
            UsableSpace = 80,
            Enabled = enabled,
            Paths = [$"p{id}"],
        };
}
