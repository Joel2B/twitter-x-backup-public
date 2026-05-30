using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.Media;

public static partial class MediaInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection AddMediaBackupPipelineInfrastructure(
        this IServiceCollection services
    )
    {
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupCalculateStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupCalculateDirectStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupApplyDirectStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupApplyStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupCheckDuplicatesStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupSetFileSizesStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupCheckIntegrityStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupFixIntegrityStep>();
        services.AddScoped<IMediaBackupPipelineStep, MediaBackupCheckIntegrityAfterFixStep>();
        return services;
    }
}
