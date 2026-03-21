using Backup.App.Data.Partition;
using Backup.App.Data.Post;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Dump;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class DumpsDataCollectionExtensions
{
    public static IServiceCollection AddDumpData(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { { "local", typeof(LocalDumpData) } };

        List<Storage> config = services
            .GetAppConfig()
            .Data.Dump.Where(o => o.Enabled && types.Keys.ToList().Contains(o.Type))
            .ToList();

        for (int i = 0; i < config.Count; i++)
        {
            Storage storage = config[i];

            string id = (i + 300).ToString();
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

                    IDumpsData? instance = (IDumpsData)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalDumpsData),
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

                    IDumpData? instance = (IDumpData)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

                    instance.Id = storage.Id;

                    return instance;
                }
            );

            services.AddScoped(sp => sp.GetRequiredKeyedService<IDumpsData>(id));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IDumpData>(id));

            if (typeof(ISetup).IsAssignableFrom(typeof(LocalDumpsData)))
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IDumpsData>(id));

            if (typeof(ISetup).IsAssignableFrom(type))
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IDumpData>(id));
        }

        return services;
    }
}
