using Backup.Application.Media.Backup;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.IO;
using Backup.Infrastructure.Media.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.Media;

public static partial class MediaBackupInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection AddMediaBackupCoreServices(this IServiceCollection services)
    {
        services.AddScoped<IMediaBackupPathAnalysisService, MediaBackupPathAnalysisService>();
        services.AddScoped<IMediaBackupPartitionPathService, MediaBackupPartitionPathService>();
        services.AddScoped<
            IMediaBackupChunkFileNamePolicyService,
            MediaBackupChunkFileNamePolicyService
        >();
        services.AddScoped<IMediaBackupChunkAssignmentService, MediaBackupChunkAssignmentService>();
        services.AddScoped<
            IMediaBackupChunkAssignmentApplyService,
            MediaBackupChunkAssignmentApplyService
        >();
        services.AddScoped<
            IMediaBackupDirectPathSelectionService,
            MediaBackupDirectPathSelectionService
        >();
        services.AddScoped<
            IMediaBackupDirectPathEligibilityService,
            MediaBackupDirectPathEligibilityService
        >();
        services.AddScoped<
            IMediaBackupDirectPathFinalizeService,
            MediaBackupDirectPathFinalizeService
        >();
        services.AddScoped<IMediaBackupSyncFinalizeService, MediaBackupSyncFinalizeService>();
        services.AddScoped<IMediaBackupDirectApplyPathService, MediaBackupDirectApplyPathService>();
        services.AddScoped<
            IMediaBackupPathObservationCompositionService,
            MediaBackupPathObservationCompositionService
        >();
        services.AddScoped<
            IMediaBackupChunkSyncPlanningService,
            MediaBackupChunkSyncPlanningService
        >();
        services.AddScoped<
            IMediaBackupIntegrityPlanningService,
            MediaBackupIntegrityPlanningService
        >();
        services.AddScoped<
            IMediaBackupIntegrityChangeDetectionService,
            MediaBackupIntegrityChangeDetectionService
        >();
        services.AddScoped<
            IMediaBackupIntegrityObservationCompositionService,
            MediaBackupIntegrityObservationCompositionService
        >();
        services.AddScoped<
            IMediaBackupIntegrityChunkDataSelectionService,
            MediaBackupIntegrityChunkDataSelectionService
        >();
        services.AddScoped<
            IMediaBackupDirectPathCandidateDecisionService,
            MediaBackupDirectPathCandidateDecisionService
        >();
        services.AddScoped<
            IMediaBackupDirectPathScanOrchestrationService,
            MediaBackupDirectPathScanOrchestrationService
        >();
        services.AddScoped<IMediaBackupDirectPathQueueService, MediaBackupDirectPathQueueService>();
        services.AddScoped<IMediaBackupProgressPolicyService, MediaBackupProgressPolicyService>();
        services.AddScoped<IMediaBackupPathProjectionService, MediaBackupPathProjectionService>();
        services.AddScoped<
            IMediaBackupArchiveMetadataMapService,
            MediaBackupArchiveMetadataMapService
        >();
        services.AddScoped<
            IMediaBackupPathArchiveMetadataProjectionService,
            MediaBackupPathArchiveMetadataProjectionService
        >();
        services.AddScoped<
            IMediaBackupPathCandidateCompositionService,
            MediaBackupPathCandidateCompositionService
        >();
        services.AddScoped<
            IMediaBackupChunkFailurePolicyService,
            MediaBackupChunkFailurePolicyService
        >();
        services.AddScoped<
            IMediaBackupChunkFailureOrchestrationService,
            MediaBackupChunkFailureOrchestrationService
        >();
        services.AddScoped<
            IMediaBackupChunkFailureApplyService,
            MediaBackupChunkFailureApplyService
        >();
        services.AddScoped<
            IMediaBackupChunkMetadataOrchestrationService,
            MediaBackupChunkMetadataOrchestrationService
        >();
        services.AddScoped<
            IMediaBackupIntegrityChunkUpdateOrchestrationService,
            MediaBackupIntegrityChunkUpdateOrchestrationService
        >();
        services.AddScoped<
            IMediaBackupIntegrityChunkApplyService,
            MediaBackupIntegrityChunkApplyService
        >();
        services.AddScoped<
            IMediaBackupIntegrityChunkRefreshService,
            MediaBackupIntegrityChunkRefreshService
        >();
        services.AddScoped<
            IMediaBackupDuplicateCleanupService,
            MediaBackupDuplicateCleanupService
        >();
        services.AddScoped<
            IMediaBackupDuplicateCheckPlanningService,
            MediaBackupDuplicateCheckPlanningService
        >();
        services.AddScoped<
            IMediaBackupDuplicateChunkOrchestrationService,
            MediaBackupDuplicateChunkOrchestrationService
        >();
        services.AddScoped<
            IMediaBackupDuplicateChunkExecutionService,
            MediaBackupDuplicateChunkExecutionService
        >();
        services.AddScoped<
            IMediaBackupPhaseOrchestrationService,
            MediaBackupPhaseOrchestrationService
        >();
        services.AddScoped<
            IMediaBackupPipelineStepCompositionService,
            MediaBackupPipelineStepCompositionService
        >();
        services.AddScoped<
            IMediaBackupChunkReconciliationService,
            MediaBackupChunkReconciliationService
        >();
        services.AddScoped<
            IMediaBackupStorageConsistencyDecisionService,
            MediaBackupStorageConsistencyDecisionService
        >();
        services.AddScoped<IMediaBackupApplyFinalizeService, MediaBackupApplyFinalizeService>();
        services.AddScoped<
            IMediaBackupApplyEntrySelectionService,
            MediaBackupApplyEntrySelectionService
        >();
        services.AddScoped<
            IMediaBackupApplyChunkPlanningService,
            MediaBackupApplyChunkPlanningService
        >();
        services.AddScoped<IMediaBackupChunkPlanningService, MediaBackupChunkPlanningService>();
        services.AddScoped<
            IMediaBackupChunkSnapshotCompositionService,
            MediaBackupChunkSnapshotCompositionService
        >();
        services.AddScoped<
            IMediaBackupChunkRuntimeCompositionService,
            MediaBackupChunkRuntimeCompositionService
        >();
        services.AddScoped<IMediaBackupChunkCountDeltaService, MediaBackupChunkCountDeltaService>();
        services.AddScoped<
            IMediaBackupChunkDeltaInputCompositionService,
            MediaBackupChunkDeltaInputCompositionService
        >();
        services.AddScoped<
            IMediaBackupChunkDeltaLogPlanningService,
            MediaBackupChunkDeltaLogPlanningService
        >();
        services.AddScoped<
            IMediaBackupCalculateExecutionService,
            MediaBackupCalculateExecutionService
        >();
        services.AddScoped<IMediaBackupChunkEntryStateService, MediaBackupChunkEntryStateService>();
        services.AddScoped<
            IMediaBackupChunkEntryStateMutationService,
            MediaBackupChunkEntryStateMutationService
        >();
        services.AddScoped<
            IMediaBackupChunkEntryStateOrchestrationService,
            MediaBackupChunkEntryStateOrchestrationService
        >();
        services.AddScoped<
            IMediaBackupChunkHashPreparationService,
            MediaBackupChunkHashPreparationService
        >();
        services.AddScoped<
            IMediaBackupChunkMetadataPolicyService,
            MediaBackupChunkMetadataPolicyService
        >();
        services.AddScoped<
            IMediaBackupChunkMetadataRefreshPlanningService,
            MediaBackupChunkMetadataRefreshPlanningService
        >();
        services.AddScoped<
            IMediaBackupChunkMetadataObservationCompositionService,
            MediaBackupChunkMetadataObservationCompositionService
        >();
        services.AddScoped<
            IMediaBackupChunkMetadataRefreshExecutionService,
            MediaBackupChunkMetadataRefreshExecutionService
        >();
        services.AddScoped<
            IMediaBackupChunkLoadDecisionService,
            MediaBackupChunkLoadDecisionService
        >();
        services.AddScoped<
            IMediaBackupChunkReadFailurePolicyService,
            MediaBackupChunkReadFailurePolicyService
        >();
        services.AddScoped<
            IMediaBackupChunkLoadExecutionService,
            MediaBackupChunkLoadExecutionService
        >();
        services.AddScoped<
            IMediaBackupChunkReportObservationAggregationService,
            MediaBackupChunkReportObservationAggregationService
        >();
        services.AddScoped<IMediaBackupChunkReportService, MediaBackupChunkReportService>();
        services.AddScoped<IMediaBackupRuntimeFactory, MediaBackupRuntimeFactory>();
        services.AddScoped<IMediaBackupPipelinePlanService, MediaBackupPipelinePlanner>();
        services.AddScoped<MediaBackupChunkStateRuntimeAdapter>();
        services.AddScoped<MediaBackupChunkRecoveryCoordinator>();
        services.AddScoped<MediaBackupChunkZipCoordinator>();
        services.AddScoped<MediaBackupChunkReportCoordinator>();
        services.AddScoped<MediaBackupApplyChunkCoordinator>();
        services.AddScoped<MediaBackupChunkSyncMutationCoordinator>();
        services.AddScoped<MediaBackupCalculateInputBuilder>();
        services.AddScoped<MediaBackupCalculateResultApplier>();
        services.AddScoped<MediaBackupDirectPathScanCoordinator>();
        services.AddScoped<
            IMediaBackupZipEntryReaderIOService,
            MediaBackupZipEntryReaderIOService
        >();
        services.AddScoped<IMediaBackupZipMutationIOService, MediaBackupZipMutationIOService>();
        services.AddScoped<
            IMediaBackupChunkPersistenceIOService,
            MediaBackupChunkPersistenceIOService
        >();

        return services;
    }
}
