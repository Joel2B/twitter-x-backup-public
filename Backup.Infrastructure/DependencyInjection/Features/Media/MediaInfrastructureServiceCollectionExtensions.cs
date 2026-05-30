using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.Media;

public static partial class MediaInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMediaInfrastructure(this IServiceCollection services)
    {
        services.AddMediaDataInfrastructure();
        services.AddMediaRuntimeDomainInfrastructure();
        services.AddMediaBackupPipelineInfrastructure();
        services.AddMediaBackupInfrastructure();
        return services;
    }
}
