using Backup.Infrastructure.Core.Abstractions.Config;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Services.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Configuration;

public static class BackupConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddBackupConfiguration(
        this IServiceCollection services,
        BackupConfigurationOptions? options = null
    )
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(AppConfig)))
            return services;

        BackupConfigurationOptions resolvedOptions = options ?? new BackupConfigurationOptions();
        string configDirectory = BackupConfigurationDirectoryResolver.Resolve(resolvedOptions);

        IAppConfigStore store = new JsonAppConfigStore(configDirectory);
        IAppConfigService configService = new AppConfigService(store);
        AppConfig config = configService.GetSnapshot().Value;

        services.AddSingleton(store);
        services.AddSingleton(configService);
        services.AddSingleton(config);

        return services;
    }
}
