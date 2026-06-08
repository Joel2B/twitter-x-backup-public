using Backup.Infrastructure.Media.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.Media;

public static partial class MediaDataInfrastructureServiceCollectionExtensions
{
    internal enum MediaCacheType
    {
        Json = 1,
        Sqlite = 2,
        Postgres = 3,
    }

    public static IServiceCollection AddMediaDataInfrastructure(this IServiceCollection services)
    {
        services.RegisterMediaDataStores();
        return services;
    }

    internal static MediaCacheType ResolveCacheType(string? cacheType)
    {
        string normalized = (cacheType ?? "json").Trim().ToLowerInvariant();

        return normalized switch
        {
            "json" or "local" or "file" => MediaCacheType.Json,
            "sqlite" => MediaCacheType.Sqlite,
            "postgres" or "postgresql" => MediaCacheType.Postgres,
            _ => throw new InvalidOperationException(
                $"Unknown media cache backend type '{cacheType}'. Allowed: json, sqlite, postgres."
            ),
        };
    }
}
