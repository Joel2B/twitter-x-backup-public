using Backup.Application.Posts;
using Backup.Application.Network;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.Posts;

public static partial class PostsInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection AddPostApplicationInfrastructure(
        this IServiceCollection services
    )
    {
        services.AddScoped<IPostExecutionService, PostExecutionService>();
        services.AddScoped<IHttpRequestHeaderPolicyService, HttpRequestHeaderPolicyService>();
        services.AddScoped<IRequestQueryStringPolicyService, RequestQueryStringPolicyService>();
        services.AddScoped<IRateLimitHeaderParserService, RateLimitHeaderParserService>();
        services.AddScoped<IRateLimitDecisionService, RateLimitDecisionService>();
        services.AddScoped<IRetryDelayPolicyService, RetryDelayPolicyService>();
        services.AddScoped<IPostRuntimeService, PostRuntimeService>();
        services.AddScoped<IPostDownloadCommandService, PostDownloadCommandService>();
        services.AddScoped<IPostRecoveryCommandService, PostRecoveryCommandService>();
        services.AddScoped<IPostDownloadFlowService, PostDownloadFlowService>();
        services.AddScoped<IPostDownloadOrchestrationService, PostDownloadOrchestrationService>();
        services.AddScoped<IPostRecoveryOrchestrationService, PostRecoveryOrchestrationService>();
        services.AddScoped<IPostReplicationService, PostReplicationService>();
        services.AddScoped<IPostRecoverySelectionService, PostRecoverySelectionService>();
        services.AddScoped<IPostMergeService, PostMergeService>();
        services.AddScoped<IPostLogFolderPolicyService, PostLogFolderPolicyService>();
        services.AddScoped<IPostSoftDeleteSelectionService, PostSoftDeleteSelectionService>();
        services.AddScoped<IPostSnapshotNormalizationService, PostSnapshotNormalizationService>();
        services.AddScoped<IPostMediaInputsCompositionService, PostMediaInputsCompositionService>();
        services.AddScoped<IPostHashingService, PostHashingService>();
        services.AddScoped<IPostDebugLogPrunePolicyService, PostDebugLogPrunePolicyService>();
        services.AddScoped<IPostHistoryPrunePolicyService, PostHistoryPrunePolicyService>();
        services.AddScoped<IPostSnapshotSizeGuardService, PostSnapshotSizeGuardService>();
        services.AddScoped<IPostProjectionComposer, PostProjectionComposer>();
        services.AddScoped<IPostIndexingService, PostIndexingService>();
        services.AddScoped<IPostStoreParityService, PostStoreParityService>();
        return services;
    }
}
