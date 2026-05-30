using System.Collections.Concurrent;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Interfaces.Data.Media;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Interfaces.Services.UtilsService;
using Backup.Infrastructure.Models.Config.Data.Backup;
using Backup.Infrastructure.Models.Media;
using Backup.Infrastructure.Models.Media.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Media;

public partial class MediaBackup(
    ILogger<MediaBackup> _logger,
    StorageBackup _config,
    IZipWriterFactory _zipWriterFactory,
    IMediaBackupData _mediaBackupData,
    IEnumerable<IMediaBackupPipelineStep> _pipelineSteps
) : IMediaBackupStrategy, IMediaBackupPipelineActions
{
    public string? Id { get; set; }

    private readonly StorageBackup _config = _config;
    private List<string> _paths = [];
    private IMediaStorage? _mediaData;
    private IMediaStorage MediaData => _mediaData ?? throw new Exception("media data not initialized");
    private readonly IMediaBackupData _mediaBackupData = _mediaBackupData;
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
    private readonly List<IntegrityChange> _changes = [];

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
