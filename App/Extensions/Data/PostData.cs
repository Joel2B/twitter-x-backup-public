using Backup.App.Data.Partition;
using Backup.App.Data.Posts;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Posts;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Posts;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class PostDataCollectionExtensions
{
    public static IServiceCollection AddPostData(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new()
        {
            ["local"] = typeof(LocalPostData),
            ["sqlite"] = typeof(SqlitePostData),
        };
        List<DataCollectionExtensions.DataRegistration<StoragePost>> registrations =
            services.ResolveRegistrations(services.GetAppConfig().Data.Post, types, keyOffset: 0);

        foreach (
            DataCollectionExtensions.DataRegistration<StoragePost> registration in registrations
        )
        {
            StoragePost storage = registration.Storage;
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

                    IPostDataStore instance = (IPostDataStore)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

                    instance.Id = registration.Id;
                    instance.IsDefault = storage.Default;

                    return instance;
                }
            );

            if (type.IsSetupType())
            {
                services.AddKeyedScoped(
                    key,
                    (sp, _) => (ISetup)sp.GetRequiredKeyedService<IPostDataStore>(key)
                );
            }

            services.AddScoped(sp => sp.GetRequiredKeyedService<IPostDataStore>(key));

            if (type.IsSetupType())
                services.AddScoped(sp => sp.GetRequiredKeyedService<ISetup>(key));
        }

        services.AddScoped<IPostData, PostDataMultiStore>();

        return services;
    }
}
