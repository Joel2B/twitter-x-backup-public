using Backup.Infrastructure.Data.Media;
using Backup.Infrastructure.Data.Partition;
using Backup.Infrastructure.Interfaces;
using Backup.Infrastructure.Interfaces.Partition;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Models.Config.Data.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class MediaDataInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMediaDataInfrastructure(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(LocalMediaData) };

        List<DataInfrastructureHelpers.DataRegistration<StorageMedia>> registrations =
            DataInfrastructureHelpers.ResolveRegistrations(
                services,
                DataInfrastructureHelpers.GetAppConfig(services).Data.Media,
                types,
                keyOffset: 200
            );

        foreach (DataInfrastructureHelpers.DataRegistration<StorageMedia> registration in registrations)
        {
            StorageMedia storage = registration.Storage;
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

                    LocalMediaCache instance = (LocalMediaCache)
                        ActivatorUtilities.CreateInstance(sp, typeof(LocalMediaCache), storage, partition);

                    return instance;
                }
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);
                    LocalMediaCache cache = sp.GetRequiredKeyedService<LocalMediaCache>(key);

                    IMediaStorage instance = (IMediaStorage)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition, cache);

                    instance.Id = registration.Id;
                    return instance;
                }
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);
                    LocalMediaCache cache = sp.GetRequiredKeyedService<LocalMediaCache>(key);

                    IMediaDataMaintenance instance = (IMediaDataMaintenance)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalMediaDataMaintenance),
                            storage,
                            partition,
                            cache
                        );

                    instance.Id = registration.Id;
                    return instance;
                }
            );

            if (DataInfrastructureHelpers.IsSetupType(type))
                services.AddKeyedScoped(key, (sp, _) => (ISetup)sp.GetRequiredKeyedService<IMediaStorage>(key));

            services.AddScoped<ISetup>(sp => sp.GetRequiredKeyedService<LocalMediaCache>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaStorage>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaDataMaintenance>(key));

            if (DataInfrastructureHelpers.IsSetupType(type))
            {
                services.AddScoped(sp => sp.GetRequiredKeyedService<ISetup>(key));
            }
        }

        return services;
    }
}



