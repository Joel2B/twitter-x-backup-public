using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Data.Partition;
using Backup.Infrastructure.DependencyInjection.Base;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Dump.Data;
using Backup.Infrastructure.Models.Config.Data.Dump;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.BulkDump;

public static partial class DumpDataInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection RegisterDumpDataStores(this IServiceCollection services)
    {
        services.AddScoped<LocalDumpDataDependencies>();

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
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    IDumpDataStore instance = (IDumpDataStore)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

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
