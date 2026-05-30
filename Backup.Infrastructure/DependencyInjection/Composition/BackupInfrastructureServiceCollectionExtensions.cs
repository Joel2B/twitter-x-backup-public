using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Composition;

public static class BackupInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBackupApiInfrastructure(this IServiceCollection services)
    {
        services.AddInfrastructureBase();
        services.AddBackupApiFeatureSet();
        return services;
    }
}
