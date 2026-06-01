using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Data.Partition;
using Backup.Infrastructure.DependencyInjection.Base;
using Backup.Infrastructure.Models.Config.Data.Posts;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Data.Json;
using Backup.Infrastructure.Posts.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.Posts;

public static partial class PostDataInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection RegisterPostDataStores(this IServiceCollection services)
    {
        services.AddScoped<LocalPostDataDependencies>();

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

        foreach (
            DataInfrastructureHelpers.DataRegistration<StoragePost> registration in registrations
        )
        {
            StoragePost storage = registration.Storage;
            string key = registration.Key;
            Type type = registration.ImplementationType;

            services.AddKeyedScoped(key, (sp, _) => CreatePartition(sp, storage));
            services.AddKeyedScoped(
                key,
                (sp, _) => CreatePostDataStore(sp, key, type, storage, registration.Id)
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
                services.AddScoped(sp => sp.GetRequiredKeyedService<ISetup>(key));
        }

        return services;
    }

    private static IPartition CreatePartition(IServiceProvider sp, StoragePost storage) =>
        (IPartition)ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage);

    private static IPostDataStore CreatePostDataStore(
        IServiceProvider sp,
        string key,
        Type storeType,
        StoragePost storage,
        string id
    )
    {
        IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

        IPostDataStore instance = (IPostDataStore)
            ActivatorUtilities.CreateInstance(sp, storeType, storage, partition);

        instance.Id = id;
        instance.IsDefault = storage.Default;

        return instance;
    }
}
