using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Models.Posts;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static partial class PostsInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection AddPostRuntimeDomainInfrastructure(
        this IServiceCollection services
    )
    {
        services.AddScoped<IPostRecovery, PostRecovery>();
        services.AddScoped<IPostDownload, PostDownload>();
        return services;
    }
}
