using Backup.Infrastructure.Data.Media;
using Backup.Infrastructure.Data.Proxy;
using Backup.Infrastructure.Bulk.Adapters;
using Backup.Infrastructure.BackupRun.Adapters;
using Backup.Infrastructure.Interfaces;
using Backup.Infrastructure.Interfaces.Data.Proxy;
using Backup.Infrastructure.Interfaces.Proxy;
using Backup.Infrastructure.Interfaces.Services.Bulk;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Interfaces.Services.UtilsService;
using Backup.Infrastructure.Services.Bulk;
using Backup.Infrastructure.Services.Media;
using Backup.Infrastructure.Services.Posts;
using Backup.Infrastructure.Services.Proxy;
using Backup.Infrastructure.Services.UtilsService;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class RuntimeInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRuntimeServicesInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IZipWriterFactory, ZipWriterFactory>();
        services.AddSingleton<IBandwidthLimiter, BandwidthLimiter>();
        services.AddScoped<IBulkRequestFactory, BulkRequestFactory>();
        services.AddScoped<IBulkSourceRouteProvider, BulkSourceRouteProvider>();
        services.AddScoped<IBulkApiClient, BulkApiClient>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IBulkService, BulkService>();

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







