using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Configuration;

public static class BackupConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddBackupConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        BackupConfigurationOptions? options = null
    ) => services.AddBackupConfigurationCore(configuration, options);

    public static IServiceCollection AddBackupConfiguration(
        this IServiceCollection services,
        BackupConfigurationOptions? options = null
    ) => services.AddBackupConfigurationCore(null, options);

    private static IServiceCollection AddBackupConfigurationCore(
        this IServiceCollection services,
        IConfiguration? configuration,
        BackupConfigurationOptions? options
    )
    {
        if (
            services.Any(descriptor => descriptor.ServiceType == typeof(BackupConfigurationRuntime))
        )
            return services;

        BackupConfigurationOptions resolvedOptions = options ?? new BackupConfigurationOptions();
        string configDirectory = BackupConfigurationDirectoryResolver.Resolve(
            resolvedOptions,
            configuration
        );

        services.AddSingleton(new BackupConfigurationRuntime { ConfigDirectory = configDirectory });

        return services;
    }
}
