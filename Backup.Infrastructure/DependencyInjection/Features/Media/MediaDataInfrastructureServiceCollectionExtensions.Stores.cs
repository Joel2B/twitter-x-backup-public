using Backup.Application.Core;
using Backup.Application.IO;
using Backup.Application.Media.Maintenance;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Data.Partition;
using Backup.Infrastructure.DependencyInjection.Base;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Data;
using Backup.Infrastructure.Models.Config.Data.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.DependencyInjection.Features.Media;

public static partial class MediaDataInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection RegisterMediaDataStores(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(LocalMediaData) };

        List<DataInfrastructureHelpers.DataRegistration<StorageMedia>> registrations =
            DataInfrastructureHelpers.ResolveRegistrations(
                services,
                DataInfrastructureHelpers.GetAppConfig(services).Data.Media,
                types,
                keyOffset: 200
            );

        foreach (
            DataInfrastructureHelpers.DataRegistration<StorageMedia> registration in registrations
        )
        {
            StorageMedia storage = registration.Storage;
            string key = registration.Key;
            Type type = registration.ImplementationType;

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    (IPartition)
                        ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage)
            );
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalMediaCachePathLayout(
                        storage,
                        sp.GetRequiredKeyedService<IPartition>(key),
                        sp.GetRequiredService<IDataStoreGuardService>(),
                        sp.GetRequiredService<IMediaCacheDirectoryPolicyService>()
                    )
            );
            services.AddKeyedScoped(key, (sp, _) => CreateMediaCacheTargets(sp, storage));
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalMediaCacheSnapshotCoordinator(
                        sp.GetRequiredKeyedService<IReadOnlyList<MediaCacheTargetRuntime>>(key),
                        sp.GetRequiredService<IPrimarySelectionService>(),
                        sp.GetRequiredService<IMediaCacheEntryPathPolicyService>(),
                        sp.GetRequiredService<IMediaCacheReplicationPathService>(),
                        sp.GetRequiredKeyedService<IPartition>(key),
                        sp.GetRequiredKeyedService<LocalMediaCachePathLayout>(key),
                        sp.GetRequiredService<ILogger<LocalMediaCacheSnapshotCoordinator>>()
                    )
            );
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalMediaCacheMutationApplier(
                        sp.GetRequiredService<ILogger<LocalMediaCache>>(),
                        sp.GetRequiredService<IMediaCacheRecheckMutationExecutionService>()
                    )
            );
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalMediaCacheLoadCoordinator(
                        sp.GetRequiredService<ILogger<LocalMediaCache>>(),
                        sp.GetRequiredKeyedService<IPartition>(key),
                        sp.GetRequiredService<IMediaCacheLoadExecutionService>(),
                        sp.GetRequiredService<IMediaCacheRecheckProbeExecutionService>(),
                        sp.GetRequiredService<IMediaCachePartitionSizeAggregationService>(),
                        sp.GetRequiredService<IMediaCacheEntryPathPolicyService>(),
                        sp.GetRequiredKeyedService<LocalMediaCachePathLayout>(key),
                        sp.GetRequiredKeyedService<LocalMediaCacheSnapshotCoordinator>(key),
                        sp.GetRequiredKeyedService<LocalMediaCacheMutationApplier>(key)
                    )
            );
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalMediaCacheWriteCoordinator(
                        storage,
                        sp.GetRequiredKeyedService<IPartition>(key),
                        sp.GetRequiredService<IMediaCachePartitionSelectionService>(),
                        sp.GetRequiredService<IMediaCacheWritePolicyService>(),
                        sp.GetRequiredService<IMediaCacheConflictResolutionService>(),
                        sp.GetRequiredKeyedService<LocalMediaCachePathLayout>(key),
                        sp.GetRequiredKeyedService<LocalMediaCacheSnapshotCoordinator>(key)
                    )
            );

            services.AddKeyedScoped<IMediaCache>(
                key,
                (sp, _) =>
                    new LocalMediaCache(
                        sp.GetRequiredService<IMediaCacheEntryPathPolicyService>(),
                        sp.GetRequiredKeyedService<LocalMediaCachePathLayout>(key),
                        sp.GetRequiredKeyedService<LocalMediaCacheSnapshotCoordinator>(key),
                        sp.GetRequiredKeyedService<LocalMediaCacheMutationApplier>(key),
                        sp.GetRequiredKeyedService<LocalMediaCacheLoadCoordinator>(key),
                        sp.GetRequiredKeyedService<LocalMediaCacheWriteCoordinator>(key)
                    )
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);
                    IMediaCache cache = sp.GetRequiredKeyedService<IMediaCache>(key);

                    IMediaStorage instance = (IMediaStorage)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition, cache);

                    instance.Id = registration.Id;
                    return instance;
                }
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);
                    IMediaCache cache = sp.GetRequiredKeyedService<IMediaCache>(key);

                    IMediaDataMaintenance instance = (IMediaDataMaintenance)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalMediaDataMaintenance),
                            storage,
                            partition,
                            cache
                        );

                    instance.Id = registration.Id;
                    return instance;
                }
            );

            if (DataInfrastructureHelpers.IsSetupType(type))
            {
                services.AddKeyedScoped(
                    key,
                    (sp, _) => (ISetup)sp.GetRequiredKeyedService<IMediaStorage>(key)
                );
            }

            services.AddScoped<ISetup>(sp => sp.GetRequiredKeyedService<IMediaCache>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaStorage>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaDataMaintenance>(key));

            if (DataInfrastructureHelpers.IsSetupType(type))
                services.AddScoped(sp => sp.GetRequiredKeyedService<ISetup>(key));
        }

        return services;
    }

    private static IReadOnlyList<MediaCacheTargetRuntime> CreateMediaCacheTargets(
        IServiceProvider sp,
        StorageMedia storage
    )
    {
        List<MediaCacheConfig> enabledCaches = storage.Cache.Where(cache => cache.Enabled).ToList();

        if (enabledCaches.Count == 0)
        {
            throw new InvalidOperationException(
                $"No enabled media caches are configured for store '{storage.Id ?? "unknown"}'."
            );
        }

        return enabledCaches
            .Select(
                (cache, index) =>
                {
                    string cacheKey = !string.IsNullOrWhiteSpace(cache.Id)
                        ? cache.Id!
                        : $"{cache.Type}-{index + 1}";

                    MediaCacheType cacheType = ResolveCacheType(cache.Type);

                    if (cache.Path is null && string.IsNullOrWhiteSpace(cache.ConnectionString))
                    {
                        throw new InvalidOperationException(
                            $"Media cache '{cacheKey}' for store '{storage.Id ?? "unknown"}' must configure Path or ConnectionString."
                        );
                    }

                    return new MediaCacheTargetRuntime(
                        key: cacheKey,
                        label: $"{storage.Id ?? "media"}:{cacheKey}",
                        isDefault: cache.Default,
                        type: cacheType,
                        path: cache.Path,
                        persistence: CreateMediaCachePersistenceService(
                            sp,
                            storage,
                            cache,
                            cacheType
                        )
                    );
                }
            )
            .ToList();
    }

    private static IMediaCachePersistenceIOService CreateMediaCachePersistenceService(
        IServiceProvider sp,
        StorageMedia storage,
        MediaCacheConfig cache,
        MediaCacheType cacheType
    ) =>
        cacheType switch
        {
            MediaCacheType.Json => new LocalMediaCachePersistenceIOService(
                sp.GetRequiredService<IMediaCacheJsonSnapshotService>()
            ),
            MediaCacheType.Sqlite => new SqliteMediaCachePersistenceIOService(),
            MediaCacheType.Postgres => new PostgresMediaCachePersistenceIOService(
                storage.Id ?? "unknown",
                cache
            ),
            _ => throw new InvalidOperationException(
                $"Unsupported media cache type '{cacheType}'."
            ),
        };
}
