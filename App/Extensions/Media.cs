using Backup.App.Data.Media;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Services.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class MediaCollectionExtensions
{
    public static IServiceCollection AddMedia(this IServiceCollection services)
    {
        services.AddScoped<IMediaProcessing, MediaProcessing>();
        services.AddScoped<IMediaProcessingLogger, LocalMediaProcessingLogger>();
        services.AddScoped<IMediaPrune, MediaPrune>();
        services.AddScoped<IMediaIntegrity, MediaIntegrity>();
        services.AddScoped<IMediaFilter, MediaFilter>();
        services.AddScoped<IMediaReplication, MediaReplication>();
        services.AddScoped<IMediaDownload, MediaDownload>();

        services.AddScoped<IMediaDownloader, MediaDownloaderHttp>();
        services.AddScoped<IMediaLogger, LocalMediaLogger>();
        services.AddScoped<IMediaDownloadControl, MediaDownloadControl>();

        return services;
    }
}
