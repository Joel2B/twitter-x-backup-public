using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static partial class PostsInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection AddPostAdapterInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPostReplication, PostReplicationAdapter>();
        return services;
    }
}

