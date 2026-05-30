using Backup.Infrastructure.Posts.Data.Json;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Posts.Models;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.Posts;

public static partial class PostsInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection AddPostParserInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPostLogger, LocalPostLogger>();
        services.AddScoped<IPostDownloader, PostDownloaderHttp>();
        services.AddScoped<IPostParser, PostParser>();
        services.AddScoped<IPostDomainParser, PostDomainParserAdapter>();
        services.AddScoped<IPostTweetDetailRequestFactory, PostTweetDetailRequestFactory>();
        return services;
    }
}
