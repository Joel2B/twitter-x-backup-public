using Backup.App.Data.Media;
using Backup.App.Data.Partition;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Partition;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Config.Data.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class MediaDataCollectionExtensions
{
    public static IServiceCollection AddMediaData(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(LocalMediaData) };

        List<DataCollectionExtensions.DataRegistration<StorageMedia>> registrations =
            services.ResolveRegistrations(
                services.GetAppConfig().Data.Media,
                types,
                keyOffset: 200
            );

        foreach (
            DataCollectionExtensions.DataRegistration<StorageMedia> registration in registrations
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
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    LocalMediaCache? instance = (LocalMediaCache)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalMediaCache),
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
                    LocalMediaCache cache = sp.GetRequiredKeyedService<LocalMediaCache>(key);

                    IMediaData? instance = (IMediaData)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition, cache);

                    instance.Id = registration.Id;

                    return instance;
                }
            );

            if (type.IsSetupType())
            {
                services.AddKeyedScoped(
                    key,
                    (sp, _) => (ISetup)sp.GetRequiredKeyedService<IMediaData>(key)
                );
            }

            services.AddScoped<ISetup>(sp => sp.GetRequiredKeyedService<LocalMediaCache>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaData>(key));

            if (type.IsSetupType())
                services.AddScoped(sp => sp.GetRequiredKeyedService<ISetup>(key));
        }

        return services;
    }
}
