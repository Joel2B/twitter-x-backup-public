using Backup.Infrastructure.Media.Data;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Services;
using Backup.Application.Media.Filter;
using Backup.Application.Media.Integrity;
using Backup.Application.Media.Prune;
using Backup.Infrastructure.Models.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.Media;

public static partial class MediaInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection AddMediaRuntimeDomainInfrastructure(
        this IServiceCollection services
    )
    {
        services.AddScoped<IMediaProcessing, MediaProcessing>();
        services.AddScoped<IMediaPrunePolicyService>(sp =>
        {
            AppConfig config = sp.GetRequiredService<AppConfig>();
            return new MediaPrunePolicyService(config.Downloads.Prune.Filters);
        });
        services.AddScoped<IMediaPrune, MediaPrune>();
        services.AddScoped<IMediaErrorFilterPolicyService, MediaErrorFilterPolicyService>();
        services.AddScoped<IMediaDownloadFilterPolicyService, MediaDownloadFilterPolicyService>();
        services.AddScoped<IMediaIntegrityPolicyService, MediaIntegrityPolicyService>();
        services.AddScoped<IMediaIntegrity, MediaIntegrity>();
        services.AddScoped<IMediaFilter, MediaFilter>();
        services.AddScoped<IMediaReplication, MediaReplication>();
        services.AddScoped<IMediaDownloadService, MediaDownloadService>();
        services.AddScoped<IMediaDownloader, MediaDownloaderHttp>();
        services.AddScoped<IMediaLogger, LocalMediaLogger>();
        return services;
    }
}
