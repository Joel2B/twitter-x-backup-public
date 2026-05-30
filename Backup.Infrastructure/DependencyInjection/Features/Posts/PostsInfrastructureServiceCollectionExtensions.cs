using Backup.Application.Posts;
using Backup.Infrastructure.Data.Posts;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Posts.Adapters;
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
        services.AddScoped<IPostDomainParser, PostDomainParserAdapter>();
        services.AddScoped<IPostTweetDetailRequestFactory, PostTweetDetailRequestFactory>();
        services.AddScoped<IPostRecovery, PostRecovery>();
        services.AddScoped<IPostDownload, PostDownload>();
        services.AddScoped<IPostExecutionService, PostExecutionService>();
        services.AddScoped<IPostRuntimeService, PostRuntimeService>();
        services.AddScoped<IPostDownloadFlowService, PostDownloadFlowService>();
        services.AddScoped<IPostDownloadOrchestrationService, PostDownloadOrchestrationService>();
        services.AddScoped<IPostRecoveryOrchestrationService, PostRecoveryOrchestrationService>();
        services.AddScoped<IPostReplicationService, PostReplicationService>();
        services.AddScoped<IPostRecoverySelectionService, PostRecoverySelectionService>();
        services.AddScoped<IPostProjectionComposer, PostProjectionComposer>();
        services.AddScoped<IPostIndexingService, PostIndexingService>();
        services.AddScoped<IPostStoreParityService, PostStoreParityService>();
        services.AddScoped<IPostReplication, PostReplicationAdapter>();

        return services;
    }
}



