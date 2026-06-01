using Backup.Application.Media.Backup;
using Backup.Infrastructure.Media.Abstractions.Services;

namespace Backup.Infrastructure.Media.Services;

public sealed class MediaBackupDependencies(
    IMediaBackupChunkAssignmentService mediaBackupChunkAssignmentService,
    IMediaBackupChunkAssignmentApplyService mediaBackupChunkAssignmentApplyService,
    IMediaBackupDirectPathFinalizeService mediaBackupDirectPathFinalizeService,
    IMediaBackupSyncFinalizeService mediaBackupSyncFinalizeService,
    IMediaBackupDirectApplyPathService mediaBackupDirectApplyPathService,
    IMediaBackupPathObservationCompositionService mediaBackupPathObservationCompositionService,
    IMediaBackupIntegrityPlanningService mediaBackupIntegrityPlanningService,
    IMediaBackupIntegrityChangeDetectionService mediaBackupIntegrityChangeDetectionService,
    IMediaBackupIntegrityObservationCompositionService mediaBackupIntegrityObservationCompositionService,
    IMediaBackupDirectPathScanOrchestrationService mediaBackupDirectPathScanOrchestrationService,
    IMediaBackupProgressPolicyService mediaBackupProgressPolicyService,
    IMediaBackupPathProjectionService mediaBackupPathProjectionService,
    IMediaBackupArchiveMetadataMapService mediaBackupArchiveMetadataMapService,
    IMediaBackupPathArchiveMetadataProjectionService mediaBackupPathArchiveMetadataProjectionService,
    IMediaBackupPathCandidateCompositionService mediaBackupPathCandidateCompositionService,
    IMediaBackupChunkFailureApplyService mediaBackupChunkFailureApplyService,
    IMediaBackupApplyChunkPlanningService mediaBackupApplyChunkPlanningService,
    IMediaBackupChunkPlanningService mediaBackupChunkPlanningService,
    IMediaBackupChunkSnapshotCompositionService mediaBackupChunkSnapshotCompositionService,
    IMediaBackupChunkRuntimeCompositionService mediaBackupChunkRuntimeCompositionService,
    IMediaBackupChunkCountDeltaService mediaBackupChunkCountDeltaService,
    IMediaBackupChunkDeltaInputCompositionService mediaBackupChunkDeltaInputCompositionService,
    IMediaBackupChunkDeltaLogPlanningService mediaBackupChunkDeltaLogPlanningService,
    IMediaBackupCalculateExecutionService mediaBackupCalculateExecutionService,
    IMediaBackupChunkEntryStateService mediaBackupChunkEntryStateService,
    IMediaBackupChunkEntryStateOrchestrationService mediaBackupChunkEntryStateOrchestrationService,
    IMediaBackupChunkHashPreparationService mediaBackupChunkHashPreparationService,
    IMediaBackupChunkMetadataRefreshExecutionService mediaBackupChunkMetadataRefreshExecutionService,
    IMediaBackupChunkReportObservationAggregationService mediaBackupChunkReportObservationAggregationService,
    IMediaBackupChunkReportService mediaBackupChunkReportService,
    IMediaBackupChunkMetadataOrchestrationService mediaBackupChunkMetadataOrchestrationService,
    IMediaBackupIntegrityChunkRefreshService mediaBackupIntegrityChunkRefreshService,
    IMediaBackupDuplicateChunkExecutionService mediaBackupDuplicateChunkExecutionService,
    IMediaBackupZipEntryReaderIOService mediaBackupZipEntryReaderIoService,
    IMediaBackupZipMutationIOService mediaBackupZipMutationIoService,
    IMediaBackupChunkPersistenceIOService mediaBackupChunkPersistenceIoService,
    IMediaBackupPhaseOrchestrationService mediaBackupPhaseOrchestrationService,
    IMediaBackupPipelineStepCompositionService mediaBackupPipelineStepCompositionService
)
{
    public IMediaBackupChunkAssignmentService ChunkAssignmentService { get; } =
        mediaBackupChunkAssignmentService;
    public IMediaBackupChunkAssignmentApplyService ChunkAssignmentApplyService { get; } =
        mediaBackupChunkAssignmentApplyService;
    public IMediaBackupDirectPathFinalizeService DirectPathFinalizeService { get; } =
        mediaBackupDirectPathFinalizeService;
    public IMediaBackupSyncFinalizeService SyncFinalizeService { get; } = mediaBackupSyncFinalizeService;
    public IMediaBackupDirectApplyPathService DirectApplyPathService { get; } =
        mediaBackupDirectApplyPathService;
    public IMediaBackupPathObservationCompositionService PathObservationCompositionService { get; } =
        mediaBackupPathObservationCompositionService;
    public IMediaBackupIntegrityPlanningService IntegrityPlanningService { get; } =
        mediaBackupIntegrityPlanningService;
    public IMediaBackupIntegrityChangeDetectionService IntegrityChangeDetectionService { get; } =
        mediaBackupIntegrityChangeDetectionService;
    public IMediaBackupIntegrityObservationCompositionService IntegrityObservationCompositionService { get; } =
        mediaBackupIntegrityObservationCompositionService;
    public IMediaBackupDirectPathScanOrchestrationService DirectPathScanOrchestrationService { get; } =
        mediaBackupDirectPathScanOrchestrationService;
    public IMediaBackupProgressPolicyService ProgressPolicyService { get; } = mediaBackupProgressPolicyService;
    public IMediaBackupPathProjectionService PathProjectionService { get; } = mediaBackupPathProjectionService;
    public IMediaBackupArchiveMetadataMapService ArchiveMetadataMapService { get; } =
        mediaBackupArchiveMetadataMapService;
    public IMediaBackupPathArchiveMetadataProjectionService PathArchiveMetadataProjectionService { get; } =
        mediaBackupPathArchiveMetadataProjectionService;
    public IMediaBackupPathCandidateCompositionService PathCandidateCompositionService { get; } =
        mediaBackupPathCandidateCompositionService;
    public IMediaBackupChunkFailureApplyService ChunkFailureApplyService { get; } =
        mediaBackupChunkFailureApplyService;
    public IMediaBackupApplyChunkPlanningService ApplyChunkPlanningService { get; } =
        mediaBackupApplyChunkPlanningService;
    public IMediaBackupChunkPlanningService ChunkPlanningService { get; } = mediaBackupChunkPlanningService;
    public IMediaBackupChunkSnapshotCompositionService ChunkSnapshotCompositionService { get; } =
        mediaBackupChunkSnapshotCompositionService;
    public IMediaBackupChunkRuntimeCompositionService ChunkRuntimeCompositionService { get; } =
        mediaBackupChunkRuntimeCompositionService;
    public IMediaBackupChunkCountDeltaService ChunkCountDeltaService { get; } =
        mediaBackupChunkCountDeltaService;
    public IMediaBackupChunkDeltaInputCompositionService ChunkDeltaInputCompositionService { get; } =
        mediaBackupChunkDeltaInputCompositionService;
    public IMediaBackupChunkDeltaLogPlanningService ChunkDeltaLogPlanningService { get; } =
        mediaBackupChunkDeltaLogPlanningService;
    public IMediaBackupCalculateExecutionService CalculateExecutionService { get; } =
        mediaBackupCalculateExecutionService;
    public IMediaBackupChunkEntryStateService ChunkEntryStateService { get; } =
        mediaBackupChunkEntryStateService;
    public IMediaBackupChunkEntryStateOrchestrationService ChunkEntryStateOrchestrationService { get; } =
        mediaBackupChunkEntryStateOrchestrationService;
    public IMediaBackupChunkHashPreparationService ChunkHashPreparationService { get; } =
        mediaBackupChunkHashPreparationService;
    public IMediaBackupChunkMetadataRefreshExecutionService ChunkMetadataRefreshExecutionService { get; } =
        mediaBackupChunkMetadataRefreshExecutionService;
    public IMediaBackupChunkReportObservationAggregationService ChunkReportObservationAggregationService { get; } =
        mediaBackupChunkReportObservationAggregationService;
    public IMediaBackupChunkReportService ChunkReportService { get; } = mediaBackupChunkReportService;
    public IMediaBackupChunkMetadataOrchestrationService ChunkMetadataOrchestrationService { get; } =
        mediaBackupChunkMetadataOrchestrationService;
    public IMediaBackupIntegrityChunkRefreshService IntegrityChunkRefreshService { get; } =
        mediaBackupIntegrityChunkRefreshService;
    public IMediaBackupDuplicateChunkExecutionService DuplicateChunkExecutionService { get; } =
        mediaBackupDuplicateChunkExecutionService;
    public IMediaBackupZipEntryReaderIOService ZipEntryReaderIoService { get; } =
        mediaBackupZipEntryReaderIoService;
    public IMediaBackupZipMutationIOService ZipMutationIoService { get; } = mediaBackupZipMutationIoService;
    public IMediaBackupChunkPersistenceIOService ChunkPersistenceIoService { get; } =
        mediaBackupChunkPersistenceIoService;
    public IMediaBackupPhaseOrchestrationService PhaseOrchestrationService { get; } =
        mediaBackupPhaseOrchestrationService;
    public IMediaBackupPipelineStepCompositionService PipelineStepCompositionService { get; } =
        mediaBackupPipelineStepCompositionService;
}
