using System.Collections.Concurrent;
using Backup.App.Extensions;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.UtilsService;
using Backup.App.Models.Media;
using Backup.App.Models.Media.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Media;

public partial class MediaBackup(
    ILogger<MediaBackup> _logger,
    Models.Config.Data.Backup.Storage _config,
    IZipWriterFactory _zipWriterFactory,
    IMediaData _mediaData,
    Interfaces.Data.Media.IMediaBackup _mediaBackup
) : IMediaBackup
{
    public string? Id { get; set; }

    private readonly Models.Config.Data.Backup.Storage _config = _config;
    private List<string> _paths = [];
    private readonly IMediaData _mediaData = _mediaData;
    private readonly Interfaces.Data.Media.IMediaBackup _mediaBackup = _mediaBackup;
    private Models.Media.Backup.Backup _backup = new()
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

    private readonly bool _stop = false;

    public async Task Backup(List<Download> downloads)
    {
        using (_logger.LogTimer(Id, "processing paths"))
            _paths = [.. downloads.SelectMany(o => o.Data).Select(o => o.Path)];

        Models.Media.Backup.Backup? backup = await _mediaBackup.GetBackup();

        if (backup is not null)
            _backup = backup;

        _backup.Chunks.Total = _config.Chunk.Count;
        _backup.Chunks.Path.Increase = _config.Chunk.Path.Increase;

        using (_logger.LogTimer(Id, "processing chunks"))
        {
            List<Chunk>? chunks = await _mediaBackup.GetChunks();
            _chunks = chunks?.ToDictionary(o => o.Id) ?? [];
        }

        using (_logger.LogTimer(Id, "calculate"))
            await Calculate();

        using (_logger.LogTimer(Id, "calculate direct"))
            await CalculateDirect();

        using (_logger.LogTimer(Id, "apply direct"))
            await ApplyDirect();

        using (_logger.LogTimer(Id, "apply"))
            await Apply();

        using (_logger.LogTimer(Id, "check duplicates"))
            await CheckDuplicates();

        using (_logger.LogTimer(Id, "set files sizes"))
            await SetFileSizes();

        if (_stop)
            return;

        using (_logger.LogTimer(Id, "check integrity"))
            await CheckIntegrity();

        using (_logger.LogTimer(Id, "fix integrity"))
            await FixIntegrity();

        using (_logger.LogTimer(Id, "check integrity"))
            await CheckIntegrity();
    }
}
