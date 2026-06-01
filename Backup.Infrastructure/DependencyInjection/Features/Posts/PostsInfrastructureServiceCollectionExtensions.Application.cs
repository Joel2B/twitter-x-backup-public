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
        services.AddScoped<IPostHistoryPrunePlanningService, PostHistoryPrunePlanningService>();
        services.AddScoped<IPostHistoryLatestSelectionService, PostHistoryLatestSelectionService>();
        services.AddScoped<IPostSnapshotVerificationPlanningService, PostSnapshotVerificationPlanningService>();
        services.AddScoped<IPostDataReplicationPlanningService, PostDataReplicationPlanningService>();
        services.AddScoped<IPostSnapshotSizeGuardService, PostSnapshotSizeGuardService>();
        services.AddScoped<IPostProjectionComposer, PostProjectionComposer>();
        services.AddScoped<IPostIndexingService, PostIndexingService>();
        services.AddScoped<IPostStoreParityService, PostStoreParityService>();
        services.AddScoped<IPostStoreParityReportService, PostStoreParityReportService>();
        services.AddScoped<IPostStoreCountsAggregationService, PostStoreCountsAggregationService>();
        services.AddScoped<IPostChangeComputationService, PostChangeComputationService>();
        services.AddScoped<IPostTimelineExtractionService, PostTimelineExtractionService>();
        services.AddScoped<IPostUserParsePolicyService, PostUserParsePolicyService>();
        services.AddScoped<IPostProjectionParseService, PostProjectionParseService>();
        services.AddScoped<IPostTokenMaterializationService, PostTokenMaterializationService>();
        services.AddScoped<IPostIdentifierFilterService, PostIdentifierFilterService>();
        services.AddScoped<IPostMergeResolutionService, PostMergeResolutionService>();
        services.AddScoped<IPostHashMetaParityService, PostHashMetaParityService>();
        services.AddScoped<IPostMetaNormalizationService, PostMetaNormalizationService>();
        services.AddScoped<IPostMetaReconciliationService, PostMetaReconciliationService>();
        services.AddScoped<IPostTableProjectionService, PostTableProjectionService>();
        services.AddScoped<IPostTableMaterializationService, PostTableMaterializationService>();
        services.AddScoped<IPostProfileCountAggregationService, PostProfileCountAggregationService>();
        services.AddScoped<IPostMetaConsistencyValidationService, PostMetaConsistencyValidationService>();
        services.AddScoped<IPostHistoryPathExtractionService, PostHistoryPathExtractionService>();
        return services;
    }
}
