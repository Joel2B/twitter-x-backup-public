using Backup.Infrastructure.Data.Posts;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Posts;
using Backup.Infrastructure.Services.Posts;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class PostsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPostsInfrastructure(this IServiceCollection services)
    {
        services.AddPostDataInfrastructure();
        services.AddScoped<IPostLogger, LocalPostLogger>();
        services.AddScoped<IPostDownloader, PostDownloaderHttp>();
        services.AddScoped<IPostParser, PostParser>();
        services.AddScoped<IPostRecovery, PostRecovery>();
        services.AddScoped<IPostDownload, PostDownload>();
        services.AddScoped<IPostReplication, PostReplication>();

        return services;
    }
}



