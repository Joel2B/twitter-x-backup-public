using Backup.Infrastructure.Data.Media;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Services.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class MediaInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMediaInfrastructure(this IServiceCollection services)
    {
        services.AddMediaDataInfrastructure();
        services.AddScoped<IMediaProcessing, MediaProcessing>();
        services.AddScoped<IMediaPrune, MediaPrune>();
        services.AddScoped<IMediaIntegrity, MediaIntegrity>();
        services.AddScoped<IMediaFilter, MediaFilter>();
        services.AddScoped<IMediaReplication, MediaReplication>();
        services.AddScoped<IMediaDownload, MediaDownload>();
        services.AddScoped<IMediaDownloader, MediaDownloaderHttp>();
        services.AddScoped<IMediaLogger, LocalMediaLogger>();
        services.AddMediaBackupInfrastructure();

        return services;
    }
}


