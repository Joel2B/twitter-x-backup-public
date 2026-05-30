using Backup.Infrastructure.Data.Media;
using Backup.Infrastructure.Data.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Media.Abstractions.Data;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Data.Backup;
using Backup.Infrastructure.Services.Media;
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
                (sp, _) => CreatePartition(sp, storage)
            );

            services.AddKeyedScoped(
                key,
                (sp, _) => CreateMediaBackupData(sp, key, storage)
            );

            services.AddKeyedScoped(
                key,
                (sp, _) => CreateMediaBackupStrategy(sp, key, type, storage, registration.Id)
            );

            services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IMediaBackupData>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaBackupStrategy>(key));
        }

        return services;
    }

    private static IPartition CreatePartition(IServiceProvider sp, StorageBackup storage) =>
        (IPartition)ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage);

    private static IMediaBackupData CreateMediaBackupData(
        IServiceProvider sp,
        string key,
        StorageBackup storage
    )
    {
        IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

        IMediaBackupData instance = (IMediaBackupData)
            ActivatorUtilities.CreateInstance(sp, typeof(LocalMediaBackup), storage, partition);

        return instance;
    }

    private static IMediaBackupStrategy CreateMediaBackupStrategy(
        IServiceProvider sp,
        string key,
        Type backupType,
        StorageBackup storage,
        string id
    )
    {
        IMediaBackupData mediaBackup = sp.GetRequiredKeyedService<IMediaBackupData>(key);

        IMediaBackupStrategy instance = (IMediaBackupStrategy)
            ActivatorUtilities.CreateInstance(sp, backupType, storage, mediaBackup);

        instance.Id = id;
        return instance;
    }
}
