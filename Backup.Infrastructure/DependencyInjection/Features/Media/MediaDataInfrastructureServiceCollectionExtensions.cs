using Backup.Infrastructure.Media.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static partial class MediaDataInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMediaDataInfrastructure(this IServiceCollection services)
    {
        services.RegisterMediaDataStores();
        return services;
    }

    internal static Type ResolveCacheType(string? cacheType)
    {
        string normalized = (cacheType ?? "json").Trim().ToLowerInvariant();

        return normalized switch
        {
            "json" or "local" or "file" => typeof(LocalMediaCache),
            "redis" => throw new NotSupportedException(
                "Media cache backend 'redis' is planned but not enabled yet. Use 'json' for now."
            ),
            "postgres" or "postgresql" => throw new NotSupportedException(
                "Media cache backend 'postgres' is planned but not enabled yet. Use 'json' for now."
            ),
            _ => throw new InvalidOperationException(
                $"Unknown media cache backend type '{cacheType}'. Allowed: json, redis, postgres."
            ),
        };
    }
}
