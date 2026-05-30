using Backup.Infrastructure.BackupRun.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class RuntimeInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRuntimeServicesInfrastructure(this IServiceCollection services)
    {
        services.AddUtilityRuntimeInfrastructure();
        services.AddBulkRuntimeInfrastructure();
        services.AddPostRuntimeInfrastructure();
        services.AddMediaRuntimeInfrastructure();
        return services;
    }

    public static IServiceCollection AddSetupInfrastructure(this IServiceCollection services)
    {
        services.AddProxyDataSetupInfrastructure();
        services.AddProxyProviderSetupInfrastructure();
        services.AddMediaLoggerSetupInfrastructure();
        return services;
    }

    public static IServiceCollection AddBackupRunInfrastructure(this IServiceCollection services)
    {
        services.AddBackupRunAdapters();
        services.AddScoped<
            Backup.Application.BackupRun.IBackupRunService,
            Backup.Application.BackupRun.BackupRunService
        >();
        return services;
    }
}







