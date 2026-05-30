using Backup.Infrastructure.Posts.Abstractions.Services;
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
