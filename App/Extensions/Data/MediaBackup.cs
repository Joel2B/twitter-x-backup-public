using Backup.App.Data.Media;
using Backup.App.Data.Partition;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Partition;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Services.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class MediaBackupCollectionExtensions
{
    public static IServiceCollection AddMediaBackup(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(MediaBackup) };

        List<DataCollectionExtensions.DataRegistration<Models.Config.Data.Backup.Storage>> registrations =
            services.ResolveRegistrations(
                services.GetAppConfig().Data.Backup,
                types,
                keyOffset: 400
            );

        foreach (
            DataCollectionExtensions.DataRegistration<Models.Config.Data.Backup.Storage> registration in registrations
        )
        {
            Models.Config.Data.Backup.Storage storage = registration.Storage;
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

                    Interfaces.Data.Media.IMediaBackup? instance =
                        (Interfaces.Data.Media.IMediaBackup)
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
                    Interfaces.Data.Media.IMediaBackup mediaBackup =
                        sp.GetRequiredKeyedService<Interfaces.Data.Media.IMediaBackup>(key);

                    IMediaBackup? instance = (IMediaBackup)
                        ActivatorUtilities.CreateInstance(sp, type, storage, mediaBackup);

                    instance.Id = registration.Id;

                    return instance;
                }
            );

            services.AddScoped(sp =>
                (ISetup)sp.GetRequiredKeyedService<Interfaces.Data.Media.IMediaBackup>(key)
            );

            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaBackup>(key));
        }

        return services;
    }
}
