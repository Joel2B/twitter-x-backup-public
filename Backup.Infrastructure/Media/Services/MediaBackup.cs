using Backup.Application.IO;
using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Abstractions.Data;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Models.Config.Data.Backup;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackup(
    ILogger<MediaBackup> logger,
    StorageBackup config,
    IZipWriterFactory zipWriterFactory,
    IMediaBackupData mediaBackupData,
    IEnumerable<IMediaBackupPipelineStep> pipelineSteps,
    IDataStoreGuardService dataStoreGuardService,
    IMediaBackupChunkEntryStateOrchestrationService chunkEntryStateOrchestrationService,
    IMediaBackupChunkFailureApplyService chunkFailureApplyService,
    IMediaBackupChunkReportObservationAggregationService chunkReportObservationAggregationService,
    IMediaBackupChunkRuntimeCompositionService chunkRuntimeCompositionService,
    IMediaBackupChunkReportService chunkReportService,
    IMediaBackupPhaseOrchestrationService phaseOrchestrationService,
    IMediaBackupPipelineStepCompositionService pipelineStepCompositionService,
    IMediaBackupCalculatePhase calculatePhase,
    IMediaBackupApplyPhase applyPhase,
    IMediaBackupDuplicatePhase duplicatePhase,
    IMediaBackupMetadataPhase metadataPhase,
    IMediaBackupIntegrityPhase integrityPhase
) : IMediaBackupStrategy, IMediaBackupPipelineActions
{
    public string? Id { get; set; }

    private readonly IReadOnlyDictionary<string, IMediaBackupPipelineStep> _pipelineStepsById =
        pipelineSteps.ToDictionary(GetPipelineStepId, StringComparer.Ordinal);

    private readonly IMediaBackupPhaseOrchestrationService _phaseOrchestrationService =
        phaseOrchestrationService;
    private readonly IMediaBackupPipelineStepCompositionService _pipelineStepCompositionService =
        pipelineStepCompositionService;

    private readonly IMediaBackupCalculatePhase _calculatePhase = calculatePhase;
    private readonly IMediaBackupApplyPhase _applyPhase = applyPhase;
    private readonly IMediaBackupDuplicatePhase _duplicatePhase = duplicatePhase;
    private readonly IMediaBackupMetadataPhase _metadataPhase = metadataPhase;
    private readonly IMediaBackupIntegrityPhase _integrityPhase = integrityPhase;

    private readonly MediaBackupRuntime _runtime = new(
        logger,
        config,
        zipWriterFactory,
        mediaBackupData,
        dataStoreGuardService,
        chunkEntryStateOrchestrationService,
        chunkFailureApplyService,
        chunkReportObservationAggregationService,
        chunkRuntimeCompositionService,
        chunkReportService,
        new MediaBackupExecutionContext(
            new BackupChunks
            {
                Chunks = new()
                {
                    Total = config.Chunk.Count,
                    Path = new() { Increase = config.Chunk.Path.Increase },
                },
            }
        )
    );

    public async Task Backup(
        List<Download> downloads,
        IMediaStorage mediaData,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        _runtime.Context.MediaData = mediaData;
        LoadPaths(downloads);
        await LoadBackupState(cancellationToken);
        await LoadChunks(cancellationToken);
        await RunPipeline(cancellationToken);
    }

    private void LoadPaths(List<Download> downloads)
    {
        using (_runtime.Logger.LogTimer(Id, "processing paths"))
            _runtime.Context.Paths = [.. downloads.SelectMany(o => o.Data).Select(o => o.Path)];
    }

    private async Task LoadBackupState(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BackupChunks? backup = await _runtime.MediaBackupData.GetBackup();

        if (backup is not null)
            _runtime.Context.Backup = backup;

        _runtime.Context.Backup.Chunks.Total = _runtime.Config.Chunk.Count;
        _runtime.Context.Backup.Chunks.Path.Increase = _runtime.Config.Chunk.Path.Increase;
    }

    private async Task LoadChunks(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using (_runtime.Logger.LogTimer(Id, "processing chunks"))
        {
            List<Chunk>? chunks = await _runtime.MediaBackupData.GetChunks();
            _runtime.Context.Chunks = chunks?.ToDictionary(o => o.Id) ?? [];
        }
    }

    private async Task RunPipeline(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MediaBackupPhaseExecutionStep> plan =
            _phaseOrchestrationService.BuildExecutionPlan(
                _pipelineStepCompositionService.BuildPhaseSteps(
                    _pipelineStepsById.Values.Select(
                        step => new MediaBackupPipelineStepDescriptorInput
                        {
                            StepId = GetPipelineStepId(step),
                            Order = step.Order,
                            TimerName = step.TimerName,
                            SkipWhenStopped = step.SkipWhenStopped,
                        }
                    )
                ),
                _runtime.Stop
            );

        foreach (MediaBackupPhaseExecutionStep planStep in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IMediaBackupPipelineStep step = _pipelineStepsById[planStep.StepId];

            using (_runtime.Logger.LogTimer(Id, planStep.TimerName))
                await step.Execute(this, cancellationToken);
        }
    }

    private static string GetPipelineStepId(IMediaBackupPipelineStep step) =>
        step.GetType().FullName ?? step.GetType().Name;

    bool IMediaBackupPipelineActions.ShouldStop => _runtime.Stop;

    Task IMediaBackupPipelineActions.CalculateAsync(CancellationToken cancellationToken) =>
        _calculatePhase.Calculate(_runtime, Id, cancellationToken);

    Task IMediaBackupPipelineActions.CalculateDirectAsync(CancellationToken cancellationToken) =>
        _calculatePhase.CalculateDirect(_runtime, cancellationToken);

    Task IMediaBackupPipelineActions.ApplyDirectAsync(CancellationToken cancellationToken) =>
        _applyPhase.ApplyDirect(_runtime, cancellationToken);

    Task IMediaBackupPipelineActions.ApplyAsync(CancellationToken cancellationToken) =>
        _applyPhase.Apply(_runtime, Id, cancellationToken);

    Task IMediaBackupPipelineActions.CheckDuplicatesAsync(CancellationToken cancellationToken) =>
        _duplicatePhase.CheckDuplicates(_runtime, cancellationToken);

    Task IMediaBackupPipelineActions.SetFileSizesAsync(CancellationToken cancellationToken) =>
        _metadataPhase.SetFileSizes(_runtime, cancellationToken);

    Task IMediaBackupPipelineActions.CheckIntegrityAsync(CancellationToken cancellationToken) =>
        _integrityPhase.CheckIntegrity(_runtime, cancellationToken);

    Task IMediaBackupPipelineActions.FixIntegrityAsync(CancellationToken cancellationToken) =>
        _integrityPhase.FixIntegrity(_runtime, cancellationToken);
}
