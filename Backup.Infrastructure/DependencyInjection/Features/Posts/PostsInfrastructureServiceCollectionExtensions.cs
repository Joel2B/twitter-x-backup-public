using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.Posts;

public static partial class PostsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPostsInfrastructure(this IServiceCollection services)
    {
        services.AddPostDataInfrastructure();
        services.AddPostParserInfrastructure();
        services.AddPostApplicationInfrastructure();
        services.AddPostAdapterInfrastructure();

        return services;
    }
}
