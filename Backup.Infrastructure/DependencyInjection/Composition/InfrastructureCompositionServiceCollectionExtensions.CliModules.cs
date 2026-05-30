using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static partial class InfrastructureCompositionServiceCollectionExtensions
{
    private static IServiceCollection AddCliFeatureDataModules(this IServiceCollection services)
    {
        services.AddPostsInfrastructure();
        services.AddDumpInfrastructure();
        services.AddBulkInfrastructure();
        services.AddMediaInfrastructure();
        return services;
    }

    private static IServiceCollection AddCliRuntimeModules(this IServiceCollection services)
    {
        services.AddRuntimeServicesInfrastructure();
        services.AddSetupInfrastructure();
        services.AddBackupRunInfrastructure();
        return services;
    }
}
