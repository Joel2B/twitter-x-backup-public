using Backup.Infrastructure.DependencyInjection.Base;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Composition;

public static partial class InfrastructureCompositionServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureBase(
        this IServiceCollection services,
        string logPrefix,
        int retainedLogCount
    )
    {
        services.AddCoreInfrastructure();
        services.AddStructuredLoggingInfrastructure(logPrefix, retainedLogCount);
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
