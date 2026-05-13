using Backup.App.Data.Partition;
using Backup.App.Data.Post;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Dump;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class DumpsDataCollectionExtensions
{
    public static IServiceCollection AddDumpData(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(LocalDumpData) };
        List<DataCollectionExtensions.DataRegistration<Storage>> registrations =
            services.ResolveRegistrations(services.GetAppConfig().Data.Dump, types, keyOffset: 300);

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

                    IDumpsData? instance = (IDumpsData)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalDumpsData),
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

                    IDumpData? instance = (IDumpData)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

                    instance.Id = registration.Id;

                    return instance;
                }
            );

            services.AddScoped(sp => sp.GetRequiredKeyedService<IDumpsData>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IDumpData>(key));

            if (typeof(ISetup).IsAssignableFrom(typeof(LocalDumpsData)))
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IDumpsData>(key));

            if (type.IsSetupType())
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IDumpData>(key));
        }

        return services;
    }
}
