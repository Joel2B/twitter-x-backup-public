using Backup.App.Data.Media;
using Backup.Infrastructure.Data.Proxy;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Proxy;
using Backup.App.Interfaces.Proxy;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.Posts;
using Backup.App.Interfaces.Services.UtilsService;
using Backup.App.Services.Bulk;
using Backup.App.Services.Media;
using Backup.Infrastructure.Services.Posts;
using Backup.Infrastructure.Services.Proxy;
using Backup.Infrastructure.Services.UtilsService;
using Backup.Infrastructure.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class RuntimeInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRuntimeServicesInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IZipWriterFactory, ZipWriterFactory>();
        services.AddSingleton<IBandwidthLimiter, BandwidthLimiter>();
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

    public static IServiceCollection AddBackupRuntimeInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<BackupRuntime>();
        return services;
    }
}




