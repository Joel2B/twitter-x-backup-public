using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static partial class PostsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPostsInfrastructure(this IServiceCollection services)
    {
        services.AddPostDataInfrastructure();
        services.AddPostParserInfrastructure();
        services.AddPostRuntimeDomainInfrastructure();
        services.AddPostApplicationInfrastructure();
        services.AddPostAdapterInfrastructure();

        return services;
    }
}
