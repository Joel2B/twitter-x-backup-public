using Backup.Infrastructure.DependencyInjection.Features.Media;

namespace Backup.Tests;

public class MediaDataInfrastructureServiceCollectionExtensionsTests
{
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
}
