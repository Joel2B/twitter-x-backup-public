using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Services.Posts;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class RuntimeInfrastructurePostServiceCollectionExtensions
{
    public static IServiceCollection AddPostRuntimeInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPostService, PostService>();
        return services;
    }
}

