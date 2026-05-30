using Backup.Infrastructure.DependencyInjection.Base;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Composition;

public static partial class InfrastructureCompositionServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureBase(this IServiceCollection services)
    {
        services.AddCoreInfrastructure();
        services.AddStructuredLoggingInfrastructure();
        return services;
    }

    public static IServiceCollection AddBackupApiFeatureSet(this IServiceCollection services)
    {
        services.AddApiFeatureModules();
        return services;
    }

    public static IServiceCollection AddBackupCliFeatureSet(this IServiceCollection services)
    {
        services.AddCliFeatureDataModules();
        services.AddCliRuntimeModules();
        return services;
    }
}
