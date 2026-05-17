using Backup.App.Data.Posts;
using Backup.App.Interfaces.Data.Posts;
using Backup.App.Interfaces.Services.Posts;
using Backup.App.Models.Posts;
using Backup.App.Services.Posts;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class PostCollectionExtensions
{
    public static IServiceCollection AddPost(this IServiceCollection services)
    {
        services.AddScoped<IPostLogger, LocalPostLogger>();
        services.AddScoped<IPostDownloader, PostDownloaderHttp>();
        services.AddScoped<IPostParser, PostParser>();
        services.AddScoped<IPostRecovery, PostRecovery>();
        services.AddScoped<IPostDownload, PostDownload>();
        services.AddScoped<IPostReplication, PostReplication>();

        return services;
    }
}
