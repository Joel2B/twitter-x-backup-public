using Backup.Infrastructure.Core.Abstractions.Config;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Services.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Runtime;

public static class RuntimeInfrastructureConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddRuntimeConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        RuntimeConfigurationOptions? options = null
    ) => services.AddRuntimeConfigurationCore(configuration, options);

    public static IServiceCollection AddRuntimeConfiguration(
        this IServiceCollection services,
        RuntimeConfigurationOptions? options = null
    ) => services.AddRuntimeConfigurationCore(null, options);

    private static IServiceCollection AddRuntimeConfigurationCore(
        this IServiceCollection services,
        IConfiguration? configuration,
        RuntimeConfigurationOptions? options
    )
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(AppConfig)))
            return services;

        RuntimeConfigurationOptions resolvedOptions = options ?? new RuntimeConfigurationOptions();
        string configDirectory = RuntimeConfigurationDirectoryResolver.Resolve(
            resolvedOptions,
            configuration
        );

        IAppConfigStore store = new JsonAppConfigStore(configDirectory);
        IAppConfigService configService = new AppConfigService(store);
        AppConfig config = configService.GetSnapshot().Value;

        services.AddSingleton(store);
        services.AddSingleton(configService);
        services.AddSingleton(config);

        return services;
    }
}
