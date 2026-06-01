using System.Collections.Concurrent;
using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Application.IO;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Abstractions.Data;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Data.Backup;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

public partial class MediaBackup(
    ILogger<MediaBackup> _logger,
    StorageBackup _config,
    IZipWriterFactory _zipWriterFactory,
    IMediaBackupData _mediaBackupData,
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
    IMediaBackupPipelineStepCompositionService mediaBackupPipelineStepCompositionService,
    IEnumerable<IMediaBackupPipelineStep> _pipelineSteps,
    IDataStoreGuardService dataStoreGuardService
) : IMediaBackupStrategy, IMediaBackupPipelineActions
{
    public string? Id { get; set; }

    private readonly StorageBackup _config = _config;
    private List<string> _paths = [];
    private IMediaStorage? _mediaData;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private IMediaStorage MediaData =>
        _dataStoreGuardService.RequireInitialized(_mediaData, "media data not initialized");
    private readonly IMediaBackupData _mediaBackupData = _mediaBackupData;
    private readonly IMediaBackupChunkAssignmentService _mediaBackupChunkAssignmentService =
        mediaBackupChunkAssignmentService;
    private readonly IMediaBackupChunkAssignmentApplyService _mediaBackupChunkAssignmentApplyService =
        mediaBackupChunkAssignmentApplyService;
    private readonly IMediaBackupDirectPathFinalizeService _mediaBackupDirectPathFinalizeService =
        mediaBackupDirectPathFinalizeService;
    private readonly IMediaBackupSyncFinalizeService _mediaBackupSyncFinalizeService =
        mediaBackupSyncFinalizeService;
    private readonly IMediaBackupDirectApplyPathService _mediaBackupDirectApplyPathService =
        mediaBackupDirectApplyPathService;
    private readonly IMediaBackupPathObservationCompositionService _mediaBackupPathObservationCompositionService =
        mediaBackupPathObservationCompositionService;
    private readonly IMediaBackupIntegrityPlanningService _mediaBackupIntegrityPlanningService =
        mediaBackupIntegrityPlanningService;
    private readonly IMediaBackupIntegrityChangeDetectionService _mediaBackupIntegrityChangeDetectionService =
        mediaBackupIntegrityChangeDetectionService;
    private readonly IMediaBackupIntegrityObservationCompositionService _mediaBackupIntegrityObservationCompositionService =
        mediaBackupIntegrityObservationCompositionService;
    private readonly IMediaBackupDirectPathScanOrchestrationService _mediaBackupDirectPathScanOrchestrationService =
        mediaBackupDirectPathScanOrchestrationService;
    private readonly IMediaBackupProgressPolicyService _mediaBackupProgressPolicyService =
        mediaBackupProgressPolicyService;
    private readonly IMediaBackupPathProjectionService _mediaBackupPathProjectionService =
        mediaBackupPathProjectionService;
    private readonly IMediaBackupArchiveMetadataMapService _mediaBackupArchiveMetadataMapService =
        mediaBackupArchiveMetadataMapService;
    private readonly IMediaBackupPathArchiveMetadataProjectionService _mediaBackupPathArchiveMetadataProjectionService =
        mediaBackupPathArchiveMetadataProjectionService;
    private readonly IMediaBackupPathCandidateCompositionService _mediaBackupPathCandidateCompositionService =
        mediaBackupPathCandidateCompositionService;
    private readonly IMediaBackupChunkFailureApplyService _mediaBackupChunkFailureApplyService =
        mediaBackupChunkFailureApplyService;
    private readonly IMediaBackupApplyChunkPlanningService _mediaBackupApplyChunkPlanningService =
        mediaBackupApplyChunkPlanningService;
    private readonly IMediaBackupChunkPlanningService _mediaBackupChunkPlanningService =
        mediaBackupChunkPlanningService;
    private readonly IMediaBackupChunkSnapshotCompositionService _mediaBackupChunkSnapshotCompositionService =
        mediaBackupChunkSnapshotCompositionService;
    private readonly IMediaBackupChunkRuntimeCompositionService _mediaBackupChunkRuntimeCompositionService =
        mediaBackupChunkRuntimeCompositionService;
    private readonly IMediaBackupChunkCountDeltaService _mediaBackupChunkCountDeltaService =
        mediaBackupChunkCountDeltaService;
    private readonly IMediaBackupChunkDeltaInputCompositionService _mediaBackupChunkDeltaInputCompositionService =
        mediaBackupChunkDeltaInputCompositionService;
    private readonly IMediaBackupChunkDeltaLogPlanningService _mediaBackupChunkDeltaLogPlanningService =
        mediaBackupChunkDeltaLogPlanningService;
    private readonly IMediaBackupChunkEntryStateService _mediaBackupChunkEntryStateService =
        mediaBackupChunkEntryStateService;
    private readonly IMediaBackupChunkEntryStateOrchestrationService _mediaBackupChunkEntryStateOrchestrationService =
        mediaBackupChunkEntryStateOrchestrationService;
    private readonly IMediaBackupChunkHashPreparationService _mediaBackupChunkHashPreparationService =
        mediaBackupChunkHashPreparationService;
    private readonly IMediaBackupChunkMetadataRefreshExecutionService _mediaBackupChunkMetadataRefreshExecutionService =
        mediaBackupChunkMetadataRefreshExecutionService;
    private readonly IMediaBackupChunkReportObservationAggregationService _mediaBackupChunkReportObservationAggregationService =
        mediaBackupChunkReportObservationAggregationService;
    private readonly IMediaBackupChunkReportService _mediaBackupChunkReportService =
        mediaBackupChunkReportService;
    private readonly IMediaBackupChunkMetadataOrchestrationService _mediaBackupChunkMetadataOrchestrationService =
        mediaBackupChunkMetadataOrchestrationService;
    private readonly IMediaBackupIntegrityChunkRefreshService _mediaBackupIntegrityChunkRefreshService =
        mediaBackupIntegrityChunkRefreshService;
    private readonly IMediaBackupDuplicateChunkExecutionService _mediaBackupDuplicateChunkExecutionService =
        mediaBackupDuplicateChunkExecutionService;
    private readonly IMediaBackupZipEntryReaderIOService _mediaBackupZipEntryReaderIoService =
        mediaBackupZipEntryReaderIoService;
    private readonly IMediaBackupZipMutationIOService _mediaBackupZipMutationIoService =
        mediaBackupZipMutationIoService;
    private readonly IMediaBackupChunkPersistenceIOService _mediaBackupChunkPersistenceIoService =
        mediaBackupChunkPersistenceIoService;
    private readonly IMediaBackupPhaseOrchestrationService _mediaBackupPhaseOrchestrationService =
        mediaBackupPhaseOrchestrationService;
    private readonly IMediaBackupPipelineStepCompositionService _mediaBackupPipelineStepCompositionService =
        mediaBackupPipelineStepCompositionService;
    private BackupChunks _backup = new()
    {
        Chunks = new()
        {
            Total = _config.Chunk.Count,
            Path = new() { Increase = _config.Chunk.Path.Increase },
        },
    };

    private Dictionary<int, Chunk> _chunks = [];
    private List<string> _pathsInBoth = [];
    private ConcurrentBag<string> _pathsDirect = [];
    private readonly List<MediaBackupIntegrityChange> _changes = [];

    private readonly ILogger<MediaBackup> _logger = _logger;
    private readonly IZipWriterFactory _zipWriterFactory = _zipWriterFactory;
    private readonly IReadOnlyDictionary<string, IMediaBackupPipelineStep> _pipelineStepsById =
        _pipelineSteps.ToDictionary(GetPipelineStepId, StringComparer.Ordinal);

    private readonly bool _stop = false;

    public async Task Backup(List<Download> downloads, IMediaStorage mediaData)
    {
        SetMediaData(mediaData);
        LoadPaths(downloads);
        await LoadBackupState();
        await LoadChunks();
        await RunPipeline();
    }

    private void SetMediaData(IMediaStorage mediaData)
    {
        _mediaData = mediaData;
    }

    private void LoadPaths(List<Download> downloads)
    {
        using (_logger.LogTimer(Id, "processing paths"))
            _paths = [.. downloads.SelectMany(o => o.Data).Select(o => o.Path)];
    }

    private async Task LoadBackupState()
    {
        BackupChunks? backup = await _mediaBackupData.GetBackup();

        if (backup is not null)
            _backup = backup;

        _backup.Chunks.Total = _config.Chunk.Count;
        _backup.Chunks.Path.Increase = _config.Chunk.Path.Increase;
    }

    private async Task LoadChunks()
    {
        using (_logger.LogTimer(Id, "processing chunks"))
        {
            List<Chunk>? chunks = await _mediaBackupData.GetChunks();
            _chunks = chunks?.ToDictionary(o => o.Id) ?? [];
        }
    }

    private async Task RunPipeline()
    {
        IReadOnlyList<MediaBackupPhaseExecutionStep> plan =
            _mediaBackupPhaseOrchestrationService.BuildExecutionPlan(
                _mediaBackupPipelineStepCompositionService.BuildPhaseSteps(
                    _pipelineStepsById.Values.Select(step => new MediaBackupPipelineStepDescriptorInput
                    {
                        StepId = GetPipelineStepId(step),
                        Order = step.Order,
                        TimerName = step.TimerName,
                        SkipWhenStopped = step.SkipWhenStopped,
                    })
                ),
                _stop
            );

        foreach (MediaBackupPhaseExecutionStep planStep in plan)
        {
            IMediaBackupPipelineStep step = _pipelineStepsById[planStep.StepId];

            using (_logger.LogTimer(Id, planStep.TimerName))
                await step.Execute(this);
        }
    }

    private static string GetPipelineStepId(IMediaBackupPipelineStep step) =>
        step.GetType().FullName ?? step.GetType().Name;

    private IReadOnlyList<MediaBackupChunkEntryState> BuildChunkEntryStates(IEnumerable<ChunkData> items) =>
        _mediaBackupChunkEntryStateOrchestrationService.BuildStates(
            items.Select(ToEntryRecord)
        );

    private void ApplyChunkEntryStates(Chunk chunk, IEnumerable<MediaBackupChunkEntryState> states)
    {
        IReadOnlyList<MediaBackupChunkEntryRecord> updated =
            _mediaBackupChunkEntryStateOrchestrationService.ApplyStates(
                chunk.Data.Select(ToEntryRecord),
                states
            );

        Dictionary<string, MediaBackupChunkEntryRecord> byPath = updated.ToDictionary(
            item => item.Path,
            StringComparer.Ordinal
        );

        foreach (ChunkData data in chunk.Data)
        {
            if (!byPath.TryGetValue(data.Path, out MediaBackupChunkEntryRecord? state))
                continue;

            data.Hash = state.Hash;
            data.FileSize = state.FileSize;
            data.Crc32 = state.Crc32;
        }
    }

    private static MediaBackupChunkEntryRecord ToEntryRecord(ChunkData item) =>
        new()
        {
            Path = item.Path,
            Hash = item.Hash,
            FileSize = item.FileSize,
            Crc32 = item.Crc32,
        };

    bool IMediaBackupPipelineActions.ShouldStop => _stop;

    Task IMediaBackupPipelineActions.CalculateAsync() => Calculate();
    Task IMediaBackupPipelineActions.CalculateDirectAsync() => CalculateDirect();
    Task IMediaBackupPipelineActions.ApplyDirectAsync() => ApplyDirect();
    Task IMediaBackupPipelineActions.ApplyAsync() => Apply();
    Task IMediaBackupPipelineActions.CheckDuplicatesAsync() => CheckDuplicates();
    Task IMediaBackupPipelineActions.SetFileSizesAsync() => SetFileSizes();
    Task IMediaBackupPipelineActions.CheckIntegrityAsync() => CheckIntegrity();
    Task IMediaBackupPipelineActions.FixIntegrityAsync() => FixIntegrity();
}
