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
        Dictionary<string, Type> types = new() { { "local", typeof(LocalMediaData) } };

        List<Storage> config = services
            .GetAppConfig()
            .Data.Media.Where(o => o.Enabled && types.Keys.ToList().Contains(o.Type))
            .ToList();

        for (int i = 0; i < config.Count; i++)
        {
            Storage storage = config[i];

            string id = (i + 200).ToString();
            storage.Id ??= id;

            Type type = types["local"];

            services.AddKeyedScoped(
                id,
                (sp, _) =>
                    (IPartition)
                        ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage)
            );

            services.AddKeyedScoped(
                id,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(id);

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
                id,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(id);
                    LocalMediaCache cache = sp.GetRequiredKeyedService<LocalMediaCache>(id);

                    IMediaData? instance = (IMediaData)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition, cache);

                    instance.Id = storage.Id;

                    return instance;
                }
            );

            if (typeof(ISetup).IsAssignableFrom(type))
                services.AddKeyedScoped(
                    id,
                    (sp, _) => (ISetup)sp.GetRequiredKeyedService<IMediaData>(id)
                );

            services.AddScoped<ISetup>(sp => sp.GetRequiredKeyedService<LocalMediaCache>(id));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaData>(id));
            services.AddScoped(sp => sp.GetRequiredKeyedService<ISetup>(id));
        }

        return services;
    }
}
