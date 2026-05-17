using Backup.App.Data.Media;
using Backup.App.Data.Partition;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Partition;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Config.Data.Backup;
using Backup.App.Services.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class MediaBackupCollectionExtensions
{
    public static IServiceCollection AddMediaBackup(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(MediaBackup) };

        List<DataCollectionExtensions.DataRegistration<StorageBackup>> registrations =
            services.ResolveRegistrations(
                services.GetAppConfig().Data.Backup,
                types,
                keyOffset: 400
            );

        foreach (
            DataCollectionExtensions.DataRegistration<StorageBackup> registration in registrations
        )
        {
            StorageBackup storage = registration.Storage;
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

                    IMediaBackup? instance = (IMediaBackup)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalMediaBackup),
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
                    IMediaBackup mediaBackup = sp.GetRequiredKeyedService<IMediaBackup>(key);

                    IMediaBackup? instance = (IMediaBackup)
                        ActivatorUtilities.CreateInstance(sp, type, storage, mediaBackup);

                    instance.Id = registration.Id;

                    return instance;
                }
            );

            services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IMediaBackup>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaBackup>(key));
        }

        return services;
    }
}
