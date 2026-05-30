using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Services.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class RuntimeInfrastructureMediaServiceCollectionExtensions
{
    public static IServiceCollection AddMediaRuntimeInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IMediaService, MediaService>();
        return services;
    }
}
