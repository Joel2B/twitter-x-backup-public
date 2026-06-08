using Backup.Infrastructure.DependencyInjection.Features.BulkDump;
using Backup.Infrastructure.DependencyInjection.Features.Media;
using Backup.Infrastructure.DependencyInjection.Features.Posts;
using Backup.Infrastructure.DependencyInjection.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Composition;

public static partial class InfrastructureCompositionServiceCollectionExtensions
{
    private static IServiceCollection AddApiFeatureModules(this IServiceCollection services)
    {
        services.AddPostsInfrastructure();
        services.AddDumpInfrastructure();
        services.AddBulkInfrastructure();
        services.AddMediaInfrastructure();
        services.AddRuntimeServicesInfrastructure();
        services.AddBackupRunInfrastructure();
        services.AddApiSetupInfrastructure();
        return services;
    }
}
