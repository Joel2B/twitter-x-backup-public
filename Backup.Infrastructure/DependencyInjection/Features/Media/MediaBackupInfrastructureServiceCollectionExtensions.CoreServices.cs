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
        AddMediaBackupPathServices(services);
        AddMediaBackupDirectPathServices(services);
        AddMediaBackupIntegrityServices(services);
        AddMediaBackupDuplicateServices(services);
        AddMediaBackupPipelineServices(services);
        AddMediaBackupChunkStateServices(services);
        AddMediaBackupChunkMetadataServices(services);
        AddMediaBackupChunkLoadAndReportServices(services);
        AddMediaBackupRuntimeServices(services);
        AddMediaBackupIOServices(services);

        return services;
    }

    private static void AddMediaBackupPathServices(IServiceCollection services)
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
            IMediaBackupStorageConsistencyDecisionService,
            MediaBackupStorageConsistencyDecisionService
        >();
    }

    private static void AddMediaBackupDirectPathServices(IServiceCollection services)
    {
        services.AddScoped<
            IMediaBackupDirectPathSelectionService,
            MediaBackupDirectPathSelectionService
        >();
        services.AddScoped<
            IMediaBackupDirectPathFinalizeService,
            MediaBackupDirectPathFinalizeService
        >();
        services.AddScoped<IMediaBackupSyncFinalizeService, MediaBackupSyncFinalizeService>();
        services.AddScoped<IMediaBackupDirectPathQueueService, MediaBackupDirectPathQueueService>();
    }

    private static void AddMediaBackupIntegrityServices(IServiceCollection services)
    {
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
    }

    private static void AddMediaBackupDuplicateServices(IServiceCollection services)
    {
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
    }

    private static void AddMediaBackupPipelineServices(IServiceCollection services)
    {
        services.AddScoped<
            IMediaBackupPhaseOrchestrationService,
            MediaBackupPhaseOrchestrationService
        >();
        services.AddScoped<
            IMediaBackupChunkReconciliationService,
            MediaBackupChunkReconciliationService
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
    }

    private static void AddMediaBackupChunkStateServices(IServiceCollection services)
    {
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
    }

    private static void AddMediaBackupChunkMetadataServices(IServiceCollection services)
    {
        services.AddScoped<
            IMediaBackupChunkMetadataOrchestrationService,
            MediaBackupChunkMetadataOrchestrationService
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
    }

    private static void AddMediaBackupChunkLoadAndReportServices(IServiceCollection services)
    {
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
    }

    private static void AddMediaBackupRuntimeServices(IServiceCollection services)
    {
        services.AddScoped<MediaBackupRuntimeFactory>();
        services.AddScoped<MediaBackupPipelinePlanner>();
        services.AddScoped<MediaBackupChunkStateRuntimeAdapter>();
        services.AddScoped<MediaBackupChunkRecoveryCoordinator>();
        services.AddScoped<MediaBackupChunkZipCoordinator>();
        services.AddScoped<MediaBackupChunkReportCoordinator>();
        services.AddScoped<MediaBackupApplyChunkCoordinator>();
        services.AddScoped<MediaBackupChunkSyncMutationCoordinator>();
        services.AddScoped<MediaBackupCalculateInputBuilder>();
        services.AddScoped<MediaBackupCalculateResultApplier>();
        services.AddScoped<MediaBackupDirectPathScanCoordinator>();
    }

    private static void AddMediaBackupIOServices(IServiceCollection services)
    {
        services.AddScoped<
            IMediaBackupZipEntryReaderIOService,
            MediaBackupZipEntryReaderIOService
        >();
        services.AddScoped<IMediaBackupZipMutationIOService, MediaBackupZipMutationIOService>();
        services.AddScoped<
            IMediaBackupChunkPersistenceIOService,
            MediaBackupChunkPersistenceIOService
        >();
    }
}
