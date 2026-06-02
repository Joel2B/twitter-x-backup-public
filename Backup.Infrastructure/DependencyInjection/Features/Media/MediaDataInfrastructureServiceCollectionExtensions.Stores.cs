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
            Type cacheType = ResolveCacheType(storage.CacheBackend?.Type);

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
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalMediaCacheSnapshotCoordinator(
                        sp.GetRequiredService<IMediaCachePersistenceIOService>(),
                        sp.GetRequiredService<IMediaCacheEntryPathPolicyService>(),
                        sp.GetRequiredService<IMediaCacheReplicationPathService>(),
                        sp.GetRequiredKeyedService<IPartition>(key),
                        sp.GetRequiredKeyedService<LocalMediaCachePathLayout>(key)
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
                        sp.GetRequiredService<IMediaCacheStoredEntryProjectionService>(),
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

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IMediaCache instance = cacheType == typeof(LocalMediaCache)
                        ? new LocalMediaCache(
                            sp.GetRequiredService<IMediaCacheEntryPathPolicyService>(),
                            sp.GetRequiredKeyedService<LocalMediaCachePathLayout>(key),
                            sp.GetRequiredKeyedService<LocalMediaCacheSnapshotCoordinator>(key),
                            sp.GetRequiredKeyedService<LocalMediaCacheMutationApplier>(key),
                            sp.GetRequiredKeyedService<LocalMediaCacheLoadCoordinator>(key),
                            sp.GetRequiredKeyedService<LocalMediaCacheWriteCoordinator>(key)
                        )
                        : (IMediaCache)
                            ActivatorUtilities.CreateInstance(
                                sp,
                                cacheType,
                                sp.GetRequiredService<IMediaCacheEntryPathPolicyService>(),
                                sp.GetRequiredKeyedService<LocalMediaCachePathLayout>(key),
                                sp.GetRequiredKeyedService<LocalMediaCacheSnapshotCoordinator>(
                                    key
                                ),
                                sp.GetRequiredKeyedService<LocalMediaCacheMutationApplier>(key),
                                sp.GetRequiredKeyedService<LocalMediaCacheLoadCoordinator>(key),
                                sp.GetRequiredKeyedService<LocalMediaCacheWriteCoordinator>(key)
                            );

                    return instance;
                }
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
}
