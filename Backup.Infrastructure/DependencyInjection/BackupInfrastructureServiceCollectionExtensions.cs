using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class BackupInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBackupApiInfrastructure(this IServiceCollection services)
    {
        services.AddCoreInfrastructure();
        services.AddStructuredLoggingInfrastructure();
        services.AddPostsInfrastructure();
        services.AddSetupInfrastructure();
        return services;
    }
}
