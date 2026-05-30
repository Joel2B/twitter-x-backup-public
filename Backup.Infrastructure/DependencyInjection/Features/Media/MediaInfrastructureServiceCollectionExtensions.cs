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
        services.AddScoped<IMediaDownloadService, MediaDownloadService>();
        services.AddScoped<IMediaDownloader, MediaDownloaderHttp>();
        services.AddScoped<IMediaLogger, LocalMediaLogger>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupCalculateStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupCalculateDirectStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupApplyDirectStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupApplyStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupCheckDuplicatesStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupSetFileSizesStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupCheckIntegrityStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupFixIntegrityStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupCheckIntegrityAfterFixStep>();
        services.AddMediaBackupInfrastructure();

        return services;
    }
}


