using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Data;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Data.Partition;
using Backup.Infrastructure.DependencyInjection.Base;
using Backup.Infrastructure.Models.Config.Data.Bulk;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.BulkDump;

public static partial class BulkDataInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection RegisterBulkDataStores(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(LocalBulkData) };

        List<DataInfrastructureHelpers.DataRegistration<StorageBulk>> registrations =
            DataInfrastructureHelpers.ResolveRegistrations(
                services,
                DataInfrastructureHelpers.GetAppConfig(services).Data.Bulk,
                types,
                keyOffset: 100
            );

        foreach (
            DataInfrastructureHelpers.DataRegistration<StorageBulk> registration in registrations
        )
        {
            StorageBulk storage = registration.Storage;
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

                    IBulkSourceDataStore instance = (IBulkSourceDataStore)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalBulkSourceData),
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

                    IBulkDataStore instance = (IBulkDataStore)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

                    instance.Id = registration.Id;
                    instance.IsDefault = storage.Default;
                    return instance;
                }
            );

            services.AddScoped(sp => sp.GetRequiredKeyedService<IBulkSourceDataStore>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IBulkDataStore>(key));

            if (typeof(ISetup).IsAssignableFrom(typeof(LocalBulkSourceData)))
            {
                services.AddScoped(sp =>
                    (ISetup)sp.GetRequiredKeyedService<IBulkSourceDataStore>(key)
                );
            }

            if (DataInfrastructureHelpers.IsSetupType(type))
            {
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IBulkDataStore>(key));
            }
        }

        return services;
    }
}
