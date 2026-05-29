using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class BackupInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBackupApiInfrastructure(this IServiceCollection services)
    {
        services.AddInfrastructureBase();
        services.AddBackupApiFeatureSet();
        return services;
    }
}
