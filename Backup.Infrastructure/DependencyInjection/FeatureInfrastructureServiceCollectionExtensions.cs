using Backup.App.Data.Media;
using Backup.App.Data.Proxy;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.Posts;
using Backup.App.Interfaces.Services.UtilsService;
using Backup.App.Extensions;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Proxy;
using Backup.App.Interfaces.Proxy;
using Backup.App.Services.Proxy;
using Backup.App.Services.Bulk;
using Backup.App.Services.Media;
using Backup.App.Services.Posts;
using Backup.App.Services.UtilsService;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class FeatureInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPostsInfrastructure(this IServiceCollection services)
    {
        services.AddPostData();
        services.AddPost();
        return services;
    }

    public static IServiceCollection AddMediaInfrastructure(this IServiceCollection services)
    {
        services.AddMediaData();
        services.AddMedia();
        services.AddMediaBackup();
        return services;
    }

    public static IServiceCollection AddBulkInfrastructure(this IServiceCollection services)
    {
        services.AddBulkData();
        return services;
    }

    public static IServiceCollection AddDumpInfrastructure(this IServiceCollection services)
    {
        services.AddDumpData();
        return services;
    }

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

    public static IServiceCollection AddAppRuntimeInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<global::Backup.App.App>();
        return services;
    }
}
