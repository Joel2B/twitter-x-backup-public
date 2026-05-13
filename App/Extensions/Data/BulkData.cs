using Backup.App.Data.Bulk;
using Backup.App.Data.Partition;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Bulk;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Bulk;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class BulkDataCollectionExtensions
{
    public static IServiceCollection AddBulkData(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(LocalBulkData) };

        List<DataCollectionExtensions.DataRegistration<Storage>> registrations =
            services.ResolveRegistrations(services.GetAppConfig().Data.Bulk, types, keyOffset: 100);

        foreach (DataCollectionExtensions.DataRegistration<Storage> registration in registrations)
        {
            Storage storage = registration.Storage;
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

                    IBulkSourceData? instance = (IBulkSourceData)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalBulkSourceData),
                            storage,
                            partition
                        );

                    return instance;
                }
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    IBulkData? instance = (IBulkData)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

                    instance.Id = registration.Id;

                    return instance;
                }
            );

            services.AddScoped(sp => sp.GetRequiredKeyedService<IBulkSourceData>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IBulkData>(key));

            if (typeof(ISetup).IsAssignableFrom(typeof(LocalBulkSourceData)))
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IBulkSourceData>(key));

            if (type.IsSetupType())
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IBulkData>(key));
        }

        return services;
    }
}
