using Backup.App.Data.Partition;
using Backup.App.Data.Post;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Post;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class PostDataCollectionExtensions
{
    public static IServiceCollection AddPostData(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { { "local", typeof(LocalPostData) } };

        List<Storage> config = services
            .GetAppConfig()
            .Data.Post.Where(o => o.Enabled && types.Keys.ToList().Contains(o.Type))
            .ToList();

        for (int i = 0; i < config.Count; i++)
        {
            Storage storage = config[i];

            string id = i.ToString();
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

                    IPostData? instance = (IPostData)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

                    instance.Id = storage.Id;

                    return instance;
                }
            );

            if (typeof(ISetup).IsAssignableFrom(type))
                services.AddKeyedScoped(
                    id,
                    (sp, _) => (ISetup)sp.GetRequiredKeyedService<IPostData>(id)
                );

            services.AddScoped(sp => sp.GetRequiredKeyedService<IPostData>(id));
            services.AddScoped(sp => sp.GetRequiredKeyedService<ISetup>(id));
        }

        return services;
    }
}
