using Backup.Infrastructure.BackupRun.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Runtime;

public static class RuntimeInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRuntimeServicesInfrastructure(
        this IServiceCollection services
    )
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

    public static IServiceCollection AddApiSetupInfrastructure(this IServiceCollection services)
    {
        services.AddProxyDataSetupInfrastructure();
        services.AddProxyProviderSetupInfrastructure();
        return services;
    }

    public static IServiceCollection AddBackupRunInfrastructure(this IServiceCollection services)
    {
        services.AddBackupRunAdapters();
        services.AddScoped<
            Backup.Application.BackupRun.IBackupRunPlanBuilder,
            Backup.Application.BackupRun.BackupRunPlanBuilder
        >();
        services.AddScoped<
            Backup.Application.BackupRun.IBackupRunExecutionMapper,
            Backup.Application.BackupRun.BackupRunExecutionMapper
        >();
        services.AddScoped<
            Backup.Application.BackupRun.IBackupRunStepExecutor,
            Backup.Application.BackupRun.BackupRunStepExecutor
        >();
        services.AddScoped<
            Backup.Application.BackupRun.IBackupRunService,
            Backup.Application.BackupRun.BackupRunService
        >();
        return services;
    }
}
