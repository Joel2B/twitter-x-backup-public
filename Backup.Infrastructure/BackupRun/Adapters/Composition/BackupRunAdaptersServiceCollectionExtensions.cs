using Backup.Application.BackupRun.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.BackupRun.Adapters;

public static class BackupRunAdaptersServiceCollectionExtensions
{
    public static IServiceCollection AddBackupRunAdapters(this IServiceCollection services)
    {
        services.AddScoped<IBackupRunExecutionContextMapper, BackupRunExecutionContextMapper>();
        services.AddScoped<IPostSourceExecutionService, PostSourceExecutionServiceAdapter>();
        services.AddScoped<IPostRecoveryExecutionService, PostRecoveryExecutionServiceAdapter>();
        services.AddScoped<IMediaExecutionService, MediaExecutionServiceAdapter>();
        services.AddScoped<IBackupRunPlanProvider, BackupRunPlanProviderAdapter>();
        services.AddScoped<IPostSourceRunner, PostSourceRunnerAdapter>();
        services.AddScoped<IPostRecoveryRunner, PostRecoveryRunnerAdapter>();
        services.AddScoped<IBulkRunner, BulkRunnerAdapter>();
        services.AddScoped<IMediaRunner, MediaRunnerAdapter>();
        services.AddScoped<IPostStoreVerifier, PostStoreVerifierAdapter>();
        return services;
    }
}
