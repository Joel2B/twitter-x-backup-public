using Backup.Infrastructure.Data.Media;
using Backup.Infrastructure.DependencyInjection;

namespace Backup.Tests;

public class MediaDataInfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void ResolveCacheType_UsesLocal_WhenCacheBackendMissing()
    {
        Type type = MediaDataInfrastructureServiceCollectionExtensions.ResolveCacheType(null);

        Assert.Equal(typeof(LocalMediaCache), type);
    }

    [Fact]
    public void ResolveCacheType_UsesLocal_WhenCacheBackendJson()
    {
        Type type = MediaDataInfrastructureServiceCollectionExtensions.ResolveCacheType("json");

        Assert.Equal(typeof(LocalMediaCache), type);
    }

    [Fact]
    public void ResolveCacheType_Throws_WhenCacheBackendRedis()
    {
        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => MediaDataInfrastructureServiceCollectionExtensions.ResolveCacheType("redis")
        );

        Assert.Contains("planned but not enabled yet", ex.Message);
    }
}
