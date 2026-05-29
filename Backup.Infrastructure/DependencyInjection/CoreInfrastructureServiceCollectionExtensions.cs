using Backup.App.Data.Partition;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Config;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config;
using Backup.App.Services.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class CoreInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services)
    {
        services.AddConfigInfrastructure();
        services.AddCoreLoggingInfrastructure();
        services.AddPartitionInfrastructure();

        return services;
    }

    public static IServiceCollection AddConfigInfrastructure(this IServiceCollection services)
    {
        IAppConfigStore store = new JsonAppConfigStore();
        IAppConfigService configService = new AppConfigService(store);
        AppConfigSnapshot snapshot = configService.GetSnapshot();
        AppConfig config = snapshot.Value;

        services.AddSingleton(store);
        services.AddSingleton(configService);
        services.AddSingleton(config);

        return services;
    }

    public static IServiceCollection AddCoreLoggingInfrastructure(this IServiceCollection services)
    {
        services.AddLogging();
        return services;
    }

    public static IServiceCollection AddPartitionInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<LocalPartition>();
        services.AddSingleton<IPartition>(sp => sp.GetRequiredService<LocalPartition>());
        services.AddSingleton<ISetup>(sp => sp.GetRequiredService<LocalPartition>());

        return services;
    }
}
