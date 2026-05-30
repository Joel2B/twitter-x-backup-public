using Backup.Infrastructure.Data.Partition;
using Backup.Infrastructure.Data.Posts;
using Backup.Infrastructure.Interfaces;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Partition;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Models.Config.Data.Posts;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class PostDataInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPostDataInfrastructure(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new()
        {
            ["local"] = typeof(LocalPostData),
            ["sqlite"] = typeof(SqlitePostData),
        };

        List<DataInfrastructureHelpers.DataRegistration<StoragePost>> registrations =
            DataInfrastructureHelpers.ResolveRegistrations(
                services,
                DataInfrastructureHelpers.GetAppConfig(services).Data.Post,
                types,
                keyOffset: 0
            );

        foreach (DataInfrastructureHelpers.DataRegistration<StoragePost> registration in registrations)
        {
            StoragePost storage = registration.Storage;
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

                    IPostDataStore instance = (IPostDataStore)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

                    instance.Id = registration.Id;
                    instance.IsDefault = storage.Default;

                    return instance;
                }
            );

            if (DataInfrastructureHelpers.IsSetupType(type))
            {
                services.AddKeyedScoped(
                    key,
                    (sp, _) => (ISetup)sp.GetRequiredKeyedService<IPostDataStore>(key)
                );
            }

            services.AddScoped(sp => sp.GetRequiredKeyedService<IPostDataStore>(key));

            if (DataInfrastructureHelpers.IsSetupType(type))
            {
                services.AddScoped(sp => sp.GetRequiredKeyedService<ISetup>(key));
            }
        }

        services.AddScoped<IPostData, PostDataMultiStore>();
        services.AddScoped<IPostDomainData>(sp =>
        {
            IPostData postData = sp.GetRequiredService<IPostData>();
            return postData as IPostDomainData ?? new PostDataDomainAdapter(postData);
        });
        return services;
    }
}



