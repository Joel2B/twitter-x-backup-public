using Backup.Application.BackupRun.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.BackupRun.Adapters;

public static class BackupRunAdaptersServiceCollectionExtensions
{
    public static IServiceCollection AddBackupRunAdapters(this IServiceCollection services)
    {
        services.AddScoped<IBackupRunPlanProvider, BackupRunPlanProviderAdapter>();
        services.AddScoped<IPostSourceRunner, PostSourceRunnerAdapter>();
        services.AddScoped<IPostRecoveryRunner, PostRecoveryRunnerAdapter>();
        services.AddScoped<IBulkRunner, BulkRunnerAdapter>();
        services.AddScoped<IMediaRunner, MediaRunnerAdapter>();
        services.AddScoped<IPostStoreVerifier, PostStoreVerifierAdapter>();
        return services;
    }
}
