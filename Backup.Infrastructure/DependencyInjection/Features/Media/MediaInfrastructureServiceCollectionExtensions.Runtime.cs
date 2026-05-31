using Backup.Infrastructure.Media.Data;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Services;
using Backup.Application.Media;
using Backup.Application.Media.Ports;
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
        services.AddScoped<IMediaErrorExclusionService, MediaErrorExclusionService>();
        services.AddScoped<IMediaDownloadFilterPolicyService, MediaDownloadFilterPolicyService>();
        services.AddScoped<IMediaDownloadDataBuilderService, MediaDownloadDataBuilderService>();
        services.AddScoped<IMediaDownloadProjectionService, MediaDownloadProjectionService>();
        services.AddScoped<IMediaDuplicateFilterService, MediaDuplicateFilterService>();
        services.AddScoped<IMediaParallelDownloadPolicyService, MediaParallelDownloadPolicyService>();
        services.AddScoped<IMediaDownloadPathPriorityPolicyService, MediaDownloadPathPriorityPolicyService>();
        services.AddScoped<IMediaDownloadQueueBuilderService, MediaDownloadQueueBuilderService>();
        services.AddScoped<IMediaDownloadExceptionPolicyService, MediaDownloadExceptionPolicyService>();
        services.AddScoped<IMediaDownloadPolicyService, MediaDownloadPolicyService>();
        services.AddScoped<IMediaDownloadContentValidationPolicyService, MediaDownloadContentValidationPolicyService>();
        services.AddScoped<IMediaDownloadStreamingPolicyService, MediaDownloadStreamingPolicyService>();
        services.AddScoped<IMediaDownloadProgressPolicyService, MediaDownloadProgressPolicyService>();
        services.AddScoped<IMediaDownloadExecutionService, MediaDownloadExecutionService>();
        services.AddScoped<IMediaLogFilePolicyService, MediaLogFilePolicyService>();
        services.AddScoped<IMediaVideoVariantPolicyService, MediaVideoVariantPolicyService>();
        services.AddScoped<IMediaIntegrityPolicyService, MediaIntegrityPolicyService>();
        services.AddScoped<IMediaPruneSelectionService, MediaPruneSelectionService>();
        services.AddScoped<IMediaIntegrity, MediaIntegrity>();
        services.AddScoped<IMediaFilter, MediaFilter>();
        services.AddScoped<IMediaReplication, MediaReplication>();
        services.AddScoped<IMediaDownloadService, MediaDownloadService>();
        services.AddScoped<IMediaDownloadParallelRunner, MediaDownloadParallelRunnerAdapter>();
        services.AddScoped<IMediaDownloader, MediaDownloaderHttp>();
        services.AddScoped<IMediaLogger, LocalMediaLogger>();
        return services;
    }
}
