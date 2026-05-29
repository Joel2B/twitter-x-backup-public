using Backup.App.Data.Partition;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Config;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config;
using Backup.App.Services.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class CoreCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        IAppConfigStore store = new JsonAppConfigStore();
        IAppConfigService configService = new AppConfigService(store);
        AppConfigSnapshot snapshot = configService.GetSnapshot();
        AppConfig config = snapshot.Value;

        services.AddSingleton(store);
        services.AddSingleton(configService);
        services.AddSingleton(config);

        services.AddLogging();

        services.AddSingleton<LocalPartition>();
        services.AddSingleton<IPartition>(sp => sp.GetRequiredService<LocalPartition>());
        services.AddSingleton<ISetup>(sp => sp.GetRequiredService<LocalPartition>());

        return services;
    }
}
