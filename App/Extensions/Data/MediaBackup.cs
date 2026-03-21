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
        Dictionary<string, Type> types = new() { { "local", typeof(MediaBackup) } };

        List<Models.Config.Data.Backup.Storage> config = services
            .GetAppConfig()
            .Data.Backup.Where(o => o.Enabled && types.Keys.ToList().Contains(o.Type))
            .ToList();

        for (int i = 0; i < config.Count; i++)
        {
            Models.Config.Data.Backup.Storage storage = config[i];

            string id = (i + 400).ToString();
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
                id,
                (sp, _) =>
                {
                    IMediaData mediaData = sp.GetRequiredKeyedService<IMediaData>("200");
                    Interfaces.Data.Media.IMediaBackup mediaBackup =
                        sp.GetRequiredKeyedService<Interfaces.Data.Media.IMediaBackup>(id);

                    IMediaBackup? instance = (IMediaBackup)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            type,
                            storage,
                            mediaData,
                            mediaBackup
                        );

                    instance.Id = storage.Id;

                    return instance;
                }
            );

            services.AddScoped(sp =>
                (ISetup)sp.GetRequiredKeyedService<Interfaces.Data.Media.IMediaBackup>(id)
            );

            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaBackup>(id));
        }

        return services;
    }
}
