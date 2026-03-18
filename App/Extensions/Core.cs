using Backup.App.Data.Partition;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class CoreCollectionExtensions
{
    public static async Task<IServiceCollection> AddCore(this IServiceCollection services)
    {
        Models.Config.App config = ConfigLoader.Load();

        services.AddSingleton(config);

        services.AddLogging();
        services.AddSingleton<IPartition, LocalPartition>();

        using ServiceProvider provider = services.BuildServiceProvider();
        LocalPartition partition = (LocalPartition)provider.GetRequiredService<IPartition>();
        await partition.Setup();

        services.AddMapper();

        return services;
    }
}
