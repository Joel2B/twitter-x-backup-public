using Backup.App.Data.Post;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Post;
using Backup.App.Models.Post;
using Backup.App.Services.Post;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class PostCollectionExtensions
{
    public static IServiceCollection AddPost(this IServiceCollection services)
    {
        services.AddScoped<IPostLogger, LocalPostLogger>();
        services.AddScoped<IPostDownloader, PostDownloaderHttp>();
        services.AddScoped<IPostParser, PostParser>();
        services.AddScoped<IPostMerger, PostMerger>();
        services.AddSingleton<IPostRecovery, PostRecovery>();
        services.AddScoped<IPostDownload, PostDownload>();
        services.AddScoped<IPostReplication, PostReplication>();

        return services;
    }
}
