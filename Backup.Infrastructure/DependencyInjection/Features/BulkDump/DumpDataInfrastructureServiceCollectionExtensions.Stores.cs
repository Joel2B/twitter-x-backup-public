using Backup.Application.Core;
using Backup.Application.Dump;
using Backup.Application.IO;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Data.Partition;
using Backup.Infrastructure.DependencyInjection.Base;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Dump.Abstractions.Services;
using Backup.Infrastructure.Dump.Data;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data.Dump;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.DependencyInjection.Features.BulkDump;

public static partial class DumpDataInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection RegisterDumpDataStores(this IServiceCollection services)
    {
        services.AddScoped<IDumpPersistenceIOService, LocalDumpPersistenceIOService>();

        Dictionary<string, Type> types = new() { ["local"] = typeof(LocalDumpData) };

        List<DataInfrastructureHelpers.DataRegistration<StorageDump>> registrations =
            DataInfrastructureHelpers.ResolveRegistrations(
                services,
                DataInfrastructureHelpers.GetAppConfig(services).Data.Dump,
                types,
                keyOffset: 300
            );

        foreach (
            DataInfrastructureHelpers.DataRegistration<StorageDump> registration in registrations
        )
        {
            StorageDump storage = registration.Storage;
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
                    new LocalDumpDataPathLayout(
                        storage,
                        sp.GetRequiredKeyedService<IPartition>(key),
                        sp.GetRequiredService<IDumpPathService>(),
                        sp.GetRequiredService<IDataStoreGuardService>()
                    )
            );
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalDumpDataSessionPathResolver(
                        sp.GetRequiredService<IDumpsData>(),
                        sp.GetRequiredService<IDumpLifecycleService>(),
                        sp.GetRequiredService<IDateTimeProvider>(),
                        sp.GetRequiredKeyedService<LocalDumpDataPathLayout>(key)
                    )
            );
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalDumpDataStateCoordinator(
                        sp.GetRequiredService<AppConfig>(),
                        sp.GetRequiredService<IDumpLifecycleService>(),
                        sp.GetRequiredService<IDataStoreGuardService>(),
                        sp.GetRequiredService<IDumpPersistenceIOService>(),
                        sp.GetRequiredKeyedService<LocalDumpDataSessionPathResolver>(key)
                    )
            );
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalDumpDataReplicationCoordinator(
                        sp.GetRequiredService<ISecondaryStoreSelectionService>(),
                        sp.GetRequiredKeyedService<IPartition>(key),
                        sp.GetRequiredService<IDumpReplicationPlanningService>(),
                        sp.GetRequiredService<IDumpPersistenceIOService>(),
                        sp.GetRequiredKeyedService<LocalDumpDataPathLayout>(key),
                        sp.GetRequiredKeyedService<LocalDumpDataSessionPathResolver>(key)
                    )
            );
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalDumpDataLoadCoordinator(
                        sp.GetRequiredService<IDumpsData>(),
                        sp.GetRequiredService<IDumpContextEligibilityService>(),
                        sp.GetRequiredService<IDumpLifecycleService>(),
                        sp.GetRequiredKeyedService<LocalDumpDataStateCoordinator>(key)
                    )
            );
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalDumpDataSaveCoordinator(
                        sp.GetRequiredService<IDumpSaveExecutionService>(),
                        sp.GetRequiredService<IDateTimeProvider>(),
                        sp.GetRequiredService<IDumpPersistenceIOService>(),
                        sp.GetRequiredKeyedService<LocalDumpDataSessionPathResolver>(key),
                        sp.GetRequiredKeyedService<LocalDumpDataStateCoordinator>(key),
                        sp.GetRequiredKeyedService<LocalDumpDataReplicationCoordinator>(key)
                    )
            );
            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    new LocalDumpDataFlushCoordinator(
                        sp.GetRequiredService<IDumpsData>(),
                        storage,
                        sp.GetRequiredService<IDumpIndexLoadService>(),
                        sp.GetRequiredService<IDumpFlushOrchestrationService>(),
                        sp.GetRequiredService<IDumpPersistenceIOService>(),
                        sp.GetRequiredKeyedService<LocalDumpDataSessionPathResolver>(key)
                    )
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    IDumpsDataStore instance = (IDumpsDataStore)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalDumpsData),
                            storage,
                            partition
                        );

                    instance.IsDefault = storage.Default;
                    return instance;
                }
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IDumpDataStore instance = type == typeof(LocalDumpData)
                        ? new LocalDumpData(
                            sp.GetRequiredService<ILogger<LocalDumpData>>(),
                            sp.GetRequiredService<IDataStoreGuardService>(),
                            sp.GetRequiredKeyedService<LocalDumpDataLoadCoordinator>(key),
                            sp.GetRequiredKeyedService<LocalDumpDataSaveCoordinator>(key),
                            sp.GetRequiredKeyedService<LocalDumpDataFlushCoordinator>(key)
                        )
                        : (IDumpDataStore)ActivatorUtilities.CreateInstance(sp, type, storage);

                    instance.Id = registration.Id;
                    instance.IsDefault = storage.Default;
                    return instance;
                }
            );

            services.AddScoped(sp => sp.GetRequiredKeyedService<IDumpsDataStore>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IDumpDataStore>(key));

            if (typeof(ISetup).IsAssignableFrom(typeof(LocalDumpsData)))
            {
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IDumpsDataStore>(key));
            }

            if (DataInfrastructureHelpers.IsSetupType(type))
            {
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IDumpDataStore>(key));
            }
        }

        return services;
    }
}
