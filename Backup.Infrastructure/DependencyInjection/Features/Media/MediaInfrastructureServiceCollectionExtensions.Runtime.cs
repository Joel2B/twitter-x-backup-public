using Backup.Infrastructure.Media.Data;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.Media;

public static partial class MediaInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection AddMediaRuntimeDomainInfrastructure(
        this IServiceCollection services
    )
    {
        services.AddScoped<IMediaProcessing, MediaProcessing>();
        services.AddScoped<IMediaPrune, MediaPrune>();
        services.AddScoped<IMediaIntegrity, MediaIntegrity>();
        services.AddScoped<IMediaFilter, MediaFilter>();
        services.AddScoped<IMediaReplication, MediaReplication>();
        services.AddScoped<IMediaDownloadService, MediaDownloadService>();
        services.AddScoped<IMediaDownloader, MediaDownloaderHttp>();
        services.AddScoped<IMediaLogger, LocalMediaLogger>();
        return services;
    }
}
