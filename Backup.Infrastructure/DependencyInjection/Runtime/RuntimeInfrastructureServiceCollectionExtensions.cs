using Backup.Infrastructure.BackupRun.Adapters;
using Backup.Infrastructure.Data.Media;
using Backup.Infrastructure.Data.Proxy;
using Backup.Infrastructure.Interfaces;
using Backup.Infrastructure.Interfaces.Data.Proxy;
using Backup.Infrastructure.Interfaces.Proxy;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Services.Proxy;
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
        services.AddScoped<LocalProxyData>();
        services.AddScoped<IProxyData>(sp => sp.GetRequiredService<LocalProxyData>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<LocalProxyData>());

        services.AddScoped<ProxyProvider>();
        services.AddScoped<IProxyProvider>(sp => sp.GetRequiredService<ProxyProvider>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<ProxyProvider>());

        services.AddScoped<LocalMediaLogger>();
        services.AddScoped<IMediaLogger>(sp => sp.GetRequiredService<LocalMediaLogger>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<LocalMediaLogger>());

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







