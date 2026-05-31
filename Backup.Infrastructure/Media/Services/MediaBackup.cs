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
    IMediaBackupDuplicateCheckPlanningService mediaBackupDuplicateCheckPlanningService,
    IMediaBackupChunkAssignmentService mediaBackupChunkAssignmentService,
    IMediaBackupDirectPathSelectionService mediaBackupDirectPathSelectionService,
    IMediaBackupChunkSyncPlanningService mediaBackupChunkSyncPlanningService,
    IMediaBackupIntegrityPlanningService mediaBackupIntegrityPlanningService,
    IMediaBackupIntegrityChangeDetectionService mediaBackupIntegrityChangeDetectionService,
    IMediaBackupDirectPathCandidateDecisionService mediaBackupDirectPathCandidateDecisionService,
    IMediaBackupDirectPathQueueService mediaBackupDirectPathQueueService,
    IMediaBackupPathProjectionService mediaBackupPathProjectionService,
    IMediaBackupChunkFailurePolicyService mediaBackupChunkFailurePolicyService,
    IMediaBackupStorageConsistencyDecisionService mediaBackupStorageConsistencyDecisionService,
    IMediaBackupChunkPlanningService mediaBackupChunkPlanningService,
    IMediaBackupChunkCountDeltaService mediaBackupChunkCountDeltaService,
    IMediaBackupChunkDeltaLogPlanningService mediaBackupChunkDeltaLogPlanningService,
    IMediaBackupChunkMetadataPolicyService mediaBackupChunkMetadataPolicyService,
    IMediaBackupChunkReportService mediaBackupChunkReportService,
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
    private readonly IMediaBackupDuplicateCheckPlanningService _mediaBackupDuplicateCheckPlanningService =
        mediaBackupDuplicateCheckPlanningService;
    private readonly IMediaBackupChunkAssignmentService _mediaBackupChunkAssignmentService =
        mediaBackupChunkAssignmentService;
    private readonly IMediaBackupDirectPathSelectionService _mediaBackupDirectPathSelectionService =
        mediaBackupDirectPathSelectionService;
    private readonly IMediaBackupChunkSyncPlanningService _mediaBackupChunkSyncPlanningService =
        mediaBackupChunkSyncPlanningService;
    private readonly IMediaBackupIntegrityPlanningService _mediaBackupIntegrityPlanningService =
        mediaBackupIntegrityPlanningService;
    private readonly IMediaBackupIntegrityChangeDetectionService _mediaBackupIntegrityChangeDetectionService =
        mediaBackupIntegrityChangeDetectionService;
    private readonly IMediaBackupDirectPathCandidateDecisionService _mediaBackupDirectPathCandidateDecisionService =
        mediaBackupDirectPathCandidateDecisionService;
    private readonly IMediaBackupDirectPathQueueService _mediaBackupDirectPathQueueService =
        mediaBackupDirectPathQueueService;
    private readonly IMediaBackupPathProjectionService _mediaBackupPathProjectionService =
        mediaBackupPathProjectionService;
    private readonly IMediaBackupChunkFailurePolicyService _mediaBackupChunkFailurePolicyService =
        mediaBackupChunkFailurePolicyService;
    private readonly IMediaBackupStorageConsistencyDecisionService _mediaBackupStorageConsistencyDecisionService =
        mediaBackupStorageConsistencyDecisionService;
    private readonly IMediaBackupChunkPlanningService _mediaBackupChunkPlanningService =
        mediaBackupChunkPlanningService;
    private readonly IMediaBackupChunkCountDeltaService _mediaBackupChunkCountDeltaService =
        mediaBackupChunkCountDeltaService;
    private readonly IMediaBackupChunkDeltaLogPlanningService _mediaBackupChunkDeltaLogPlanningService =
        mediaBackupChunkDeltaLogPlanningService;
    private readonly IMediaBackupChunkMetadataPolicyService _mediaBackupChunkMetadataPolicyService =
        mediaBackupChunkMetadataPolicyService;
    private readonly IMediaBackupChunkReportService _mediaBackupChunkReportService =
        mediaBackupChunkReportService;
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
    private readonly List<IMediaBackupPipelineStep> _pipelineSteps = _pipelineSteps
        .OrderBy(step => step.Order)
        .ThenBy(step => step.TimerName, StringComparer.Ordinal)
        .ToList();

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
        foreach (IMediaBackupPipelineStep step in _pipelineSteps)
        {
            if (_stop && step.SkipWhenStopped)
                break;

            using (_logger.LogTimer(Id, step.TimerName))
                await step.Execute(this);
        }
    }

    private static void ApplyFailureStates(Chunk chunk, IReadOnlyList<MediaBackupChunkFailureState> states)
    {
        Dictionary<string, MediaBackupChunkFailureState> byPath = states.ToDictionary(item => item.Path);

        foreach (ChunkData data in chunk.Data)
        {
            if (!byPath.TryGetValue(data.Path, out MediaBackupChunkFailureState? state))
                continue;

            data.Hash = state.Hash;
            data.FileSize = state.FileSize;
            data.Crc32 = state.Crc32;
        }
    }

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
