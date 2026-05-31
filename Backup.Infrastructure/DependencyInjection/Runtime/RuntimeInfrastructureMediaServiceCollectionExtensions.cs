using Backup.Application.Media;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Runtime;

public static class RuntimeInfrastructureMediaServiceCollectionExtensions
{
    public static IServiceCollection AddMediaRuntimeInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IMediaOrchestrationService, MediaOrchestrationService>();
        services.AddScoped<IMediaOrchestrationStorageResolutionService, MediaOrchestrationStorageResolutionService>();
        services.AddScoped<MediaOrchestrationCommandAdapter>();
        services.AddScoped<IMediaService, MediaService>();
        return services;
    }
}
