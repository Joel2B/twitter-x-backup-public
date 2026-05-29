using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class InfrastructureCompositionServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureBase(this IServiceCollection services)
    {
        services.AddCoreInfrastructure();
        services.AddStructuredLoggingInfrastructure();
        return services;
    }

    public static IServiceCollection AddBackupApiFeatureSet(this IServiceCollection services)
    {
        services.AddPostsInfrastructure();
        services.AddSetupInfrastructure();
        return services;
    }

    public static IServiceCollection AddBackupCliFeatureSet(this IServiceCollection services)
    {
        services.AddPostsInfrastructure();
        services.AddDumpInfrastructure();
        services.AddBulkInfrastructure();
        services.AddMediaInfrastructure();
        services.AddRuntimeServicesInfrastructure();
        services.AddSetupInfrastructure();
        services.AddBackupRunInfrastructure();
        return services;
    }
}
