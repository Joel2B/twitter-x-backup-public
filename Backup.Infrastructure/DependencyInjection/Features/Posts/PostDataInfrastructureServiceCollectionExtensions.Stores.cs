using Backup.Application.Core;
using Backup.Application.IO;
using Backup.Application.Posts;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Data.Partition;
using Backup.Infrastructure.DependencyInjection.Base;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data.Posts;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Data.Json;
using Backup.Infrastructure.Posts.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.DependencyInjection.Features.Posts;

public static partial class PostDataInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection RegisterPostDataStores(this IServiceCollection services)
    {
        services.AddScoped(
            sp =>
                new LocalPostDataMutationCoordinator(
                    sp.GetRequiredService<IPostStoreMergeMutationService>(),
                    sp.GetRequiredService<IPostSoftDeleteExecutionService>(),
                    sp.GetRequiredService<IPostSnapshotNormalizationService>(),
                    sp.GetRequiredService<IPostChangeComputationService>(),
                    sp.GetRequiredService<IPostChangeReadModelProjectionService>()
                )
        );
        services.AddScoped(
            sp =>
                new LocalPostDataReadCoordinator(
                    sp.GetRequiredService<IPostMediaInputsCompositionService>(),
                    sp.GetRequiredService<IPostStoreCountsAggregationService>(),
                    sp.GetRequiredService<IPostProfileCountAggregationService>(),
                    sp.GetRequiredService<IPostIdentifierFilterService>()
                )
        );
        services.AddScoped(
            sp =>
                new LocalPostDataHashCoordinator(
                    sp.GetRequiredService<IPostHashingService>(),
                    sp.GetRequiredService<IPostHashMetaParityService>(),
                    sp.GetRequiredService<IPostMetaNormalizationService>(),
                    sp.GetRequiredService<IPostMetaReconciliationService>(),
                    sp.GetRequiredService<IPostMetaConsistencyValidationService>()
                )
        );
        services.AddScoped(
            sp =>
                new LocalPostDataHistoryCoordinator(
                    sp.GetRequiredService<IPostHistoryPathExtractionService>(),
                    sp.GetRequiredService<IPostHistoryPrunePlanningService>(),
                    sp.GetRequiredService<IPostSnapshotVerificationExecutionService>(),
                    sp.GetRequiredService<IPostDataReplicationPlanningService>(),
                    sp.GetRequiredService<IPostHistoryArchivePathService>(),
                    sp.GetRequiredService<IDateTimeProvider>()
                )
        );
        services.AddScoped(
            sp =>
                new LocalPostDataTableCoordinator(
                    sp.GetRequiredService<IPostTableProjectionService>(),
                    sp.GetRequiredService<IPostTableMaterializationService>()
                )
        );
        services.AddScoped<SqlitePostDataDependencies>();

        Dictionary<string, Type> types = new()
        {
            ["local"] = typeof(LocalPostData),
            ["sqlite"] = typeof(SqlitePostData),
        };

        List<DataInfrastructureHelpers.DataRegistration<StoragePost>> registrations =
            DataInfrastructureHelpers.ResolveRegistrations(
                services,
                DataInfrastructureHelpers.GetAppConfig(services).Data.Post,
                types,
                keyOffset: 0
            );

        foreach (
            DataInfrastructureHelpers.DataRegistration<StoragePost> registration in registrations
        )
        {
            StoragePost storage = registration.Storage;
            string key = registration.Key;
            Type type = registration.ImplementationType;

            services.AddKeyedScoped(key, (sp, _) => CreatePartition(sp, storage));
            services.AddKeyedScoped(
                key,
                (sp, _) => CreatePostDataStore(sp, key, type, storage, registration.Id)
            );

            if (DataInfrastructureHelpers.IsSetupType(type))
            {
                services.AddKeyedScoped(
                    key,
                    (sp, _) => (ISetup)sp.GetRequiredKeyedService<IPostDataStore>(key)
                );
            }

            services.AddScoped(sp => sp.GetRequiredKeyedService<IPostDataStore>(key));

            if (DataInfrastructureHelpers.IsSetupType(type))
                services.AddScoped(sp => sp.GetRequiredKeyedService<ISetup>(key));
        }

        return services;
    }

    private static IPartition CreatePartition(IServiceProvider sp, StoragePost storage) =>
        (IPartition)ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage);

    private static IPostDataStore CreatePostDataStore(
        IServiceProvider sp,
        string key,
        Type storeType,
        StoragePost storage,
        string id
    )
    {
        IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

        IPostDataStore instance = storeType == typeof(LocalPostData)
            ? new LocalPostData(
                sp.GetRequiredService<ILogger<LocalPostData>>(),
                sp.GetRequiredService<AppConfig>(),
                storage,
                partition,
                sp.GetRequiredService<LocalPostDataMutationCoordinator>(),
                sp.GetRequiredService<LocalPostDataReadCoordinator>(),
                sp.GetRequiredService<LocalPostDataHashCoordinator>(),
                sp.GetRequiredService<LocalPostDataHistoryCoordinator>(),
                sp.GetRequiredService<LocalPostDataTableCoordinator>(),
                sp.GetRequiredService<IDataStoreGuardService>()
            )
            : (IPostDataStore)ActivatorUtilities.CreateInstance(sp, storeType, storage, partition);

        instance.Id = id;
        instance.IsDefault = storage.Default;

        return instance;
    }
}
