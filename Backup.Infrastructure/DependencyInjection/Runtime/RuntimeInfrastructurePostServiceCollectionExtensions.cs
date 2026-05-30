using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Runtime;

public static class RuntimeInfrastructurePostServiceCollectionExtensions
{
    public static IServiceCollection AddPostRuntimeInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPostService, PostService>();
        return services;
    }
}
