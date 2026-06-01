using Backup.Application.Core;
using Backup.Application.IO;
using Backup.Application.Posts;

namespace Backup.Infrastructure.Posts.Data.Json;

public sealed class LocalPostDataDependencies(
    IPostStoreMergeMutationService postStoreMergeMutationService,
    IPostSoftDeleteExecutionService postSoftDeleteExecutionService,
    IPostSnapshotNormalizationService postSnapshotNormalizationService,
    IPostMediaInputsCompositionService postMediaInputsCompositionService,
    IPostHashingService postHashingService,
    IPostHashMetaParityService postHashMetaParityService,
    IPostMetaNormalizationService postMetaNormalizationService,
    IPostMetaReconciliationService postMetaReconciliationService,
    IPostHistoryPathExtractionService postHistoryPathExtractionService,
    IPostHistoryPrunePlanningService postHistoryPrunePlanningService,
    IPostSnapshotVerificationExecutionService postSnapshotVerificationExecutionService,
    IPostDataReplicationPlanningService postDataReplicationPlanningService,
    IPostChangeComputationService postChangeComputationService,
    IPostChangeReadModelProjectionService postChangeReadModelProjectionService,
    IPostStoreCountsAggregationService postStoreCountsAggregationService,
    IPostProfileCountAggregationService postProfileCountAggregationService,
    IPostMetaConsistencyValidationService postMetaConsistencyValidationService,
    IPostTableProjectionService postTableProjectionService,
    IPostTableMaterializationService postTableMaterializationService,
    IPostIdentifierFilterService postIdentifierFilterService,
    IDataStoreGuardService dataStoreGuardService,
    IPostHistoryArchivePathService postHistoryArchivePathService,
    IDateTimeProvider dateTimeProvider
)
{
    public IPostStoreMergeMutationService PostStoreMergeMutationService { get; } =
        postStoreMergeMutationService;
    public IPostSoftDeleteExecutionService PostSoftDeleteExecutionService { get; } =
        postSoftDeleteExecutionService;
    public IPostSnapshotNormalizationService PostSnapshotNormalizationService { get; } =
        postSnapshotNormalizationService;
    public IPostMediaInputsCompositionService PostMediaInputsCompositionService { get; } =
        postMediaInputsCompositionService;
    public IPostHashingService PostHashingService { get; } = postHashingService;
    public IPostHashMetaParityService PostHashMetaParityService { get; } = postHashMetaParityService;
    public IPostMetaNormalizationService PostMetaNormalizationService { get; } =
        postMetaNormalizationService;
    public IPostMetaReconciliationService PostMetaReconciliationService { get; } =
        postMetaReconciliationService;
    public IPostHistoryPathExtractionService PostHistoryPathExtractionService { get; } =
        postHistoryPathExtractionService;
    public IPostHistoryPrunePlanningService PostHistoryPrunePlanningService { get; } =
        postHistoryPrunePlanningService;
    public IPostSnapshotVerificationExecutionService PostSnapshotVerificationExecutionService { get; } =
        postSnapshotVerificationExecutionService;
    public IPostDataReplicationPlanningService PostDataReplicationPlanningService { get; } =
        postDataReplicationPlanningService;
    public IPostChangeComputationService PostChangeComputationService { get; } =
        postChangeComputationService;
    public IPostChangeReadModelProjectionService PostChangeReadModelProjectionService { get; } =
        postChangeReadModelProjectionService;
    public IPostStoreCountsAggregationService PostStoreCountsAggregationService { get; } =
        postStoreCountsAggregationService;
    public IPostProfileCountAggregationService PostProfileCountAggregationService { get; } =
        postProfileCountAggregationService;
    public IPostMetaConsistencyValidationService PostMetaConsistencyValidationService { get; } =
        postMetaConsistencyValidationService;
    public IPostTableProjectionService PostTableProjectionService { get; } = postTableProjectionService;
    public IPostTableMaterializationService PostTableMaterializationService { get; } =
        postTableMaterializationService;
    public IPostIdentifierFilterService PostIdentifierFilterService { get; } = postIdentifierFilterService;
    public IDataStoreGuardService DataStoreGuardService { get; } = dataStoreGuardService;
    public IPostHistoryArchivePathService PostHistoryArchivePathService { get; } =
        postHistoryArchivePathService;
    public IDateTimeProvider DateTimeProvider { get; } = dateTimeProvider;
}
