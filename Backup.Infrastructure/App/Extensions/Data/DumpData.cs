using Backup.App.Data.Partition;
using Backup.App.Data.Posts;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Posts;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Dump;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class DumpsDataCollectionExtensions
{
    public static IServiceCollection AddDumpData(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(LocalDumpData) };
        List<DataCollectionExtensions.DataRegistration<StorageDump>> registrations =
            services.ResolveRegistrations(services.GetAppConfig().Data.Dump, types, keyOffset: 300);

        foreach (
            DataCollectionExtensions.DataRegistration<StorageDump> registration in registrations
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
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IDumpsDataStore>(key));

            if (type.IsSetupType())
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IDumpDataStore>(key));
        }

        services.AddScoped<IDumpsData, DumpsDataMultiStore>();
        services.AddScoped<IDumpData, DumpDataMultiStore>();

        return services;
    }
}
