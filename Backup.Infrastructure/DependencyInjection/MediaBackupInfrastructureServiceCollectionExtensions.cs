using Backup.App.Data.Media;
using Backup.App.Data.Partition;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Media;
using Backup.App.Interfaces.Partition;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Config.Data.Backup;
using Backup.App.Services.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class MediaBackupInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMediaBackupInfrastructure(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(MediaBackup) };

        List<DataInfrastructureHelpers.DataRegistration<StorageBackup>> registrations =
            DataInfrastructureHelpers.ResolveRegistrations(
                services,
                DataInfrastructureHelpers.GetAppConfig(services).Data.Backup,
                types,
                keyOffset: 400
            );

        foreach (DataInfrastructureHelpers.DataRegistration<StorageBackup> registration in registrations)
        {
            StorageBackup storage = registration.Storage;
            string key = registration.Key;
            Type type = registration.ImplementationType;

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    (IPartition)ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage)
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    IMediaBackupData instance = (IMediaBackupData)
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
                    IMediaBackupData mediaBackup = sp.GetRequiredKeyedService<IMediaBackupData>(key);

                    IMediaBackup instance = (IMediaBackup)
                        ActivatorUtilities.CreateInstance(sp, type, storage, mediaBackup);

                    instance.Id = registration.Id;
                    return instance;
                }
            );

            services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IMediaBackupData>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaBackup>(key));
        }

        return services;
    }
}
