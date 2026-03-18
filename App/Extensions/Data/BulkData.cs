using Backup.App.Data.Bulk;
using Backup.App.Data.Partition;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Bulk;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Bulk;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class BulkDataCollectionExtensions
{
    public static IServiceCollection AddBulkData(this IServiceCollection services)
    {
        using ServiceProvider provider = services.BuildServiceProvider();
        Dictionary<string, Type> types = new() { { "local", typeof(LocalBulkData) } };

        List<Storage> config = provider
            .GetRequiredService<Models.Config.App>()
            .Data.Bulk.Where(o => o.Enabled && types.Keys.ToList().Contains(o.Type))
            .ToList();

        for (int i = 0; i < config.Count; i++)
        {
            Storage storage = config[i];

            string id = (i + 100).ToString();
            storage.Id ??= id;

            Type type = types["local"];

            services.AddKeyedScoped(
                id,
                (sp, _) =>
                {
                    return (IPartition)
                        ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage);
                }
            );

            services.AddKeyedScoped(
                id,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(id);

                    IBulkSourceData? instance = (IBulkSourceData)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalBulkSourceData),
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

                    IBulkData? instance = (IBulkData)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

                    instance.Id = storage.Id;

                    return instance;
                }
            );

            services.AddScoped(sp => sp.GetRequiredKeyedService<IBulkSourceData>(id));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IBulkData>(id));

            if (typeof(ISetup).IsAssignableFrom(typeof(LocalBulkSourceData)))
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IBulkSourceData>(id));

            if (typeof(ISetup).IsAssignableFrom(type))
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IBulkData>(id));
        }

        return services;
    }
}
