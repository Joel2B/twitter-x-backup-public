using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;
using Backup.Application.Media.Models;
using Backup.Application.Media;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Data;

public class LocalMediaDataMaintenance(
    ILogger<LocalMediaDataMaintenance> _log,
    StorageMedia _config,
    IPartition _partition,
    IMediaCache _mediaCache,
    IMediaDownloadModelMapper mediaDownloadModelMapper,
    IMediaStoragePathService mediaStoragePathService,
    IMediaMaintenanceDownloadProjectionService mediaMaintenanceDownloadProjectionService,
    IMediaMaintenanceCachedDownloadFilterService mediaMaintenanceCachedDownloadFilterService,
    IMediaMaintenanceIntegrityObservationCompositionService mediaMaintenanceIntegrityObservationCompositionService,
    IMediaMaintenanceIntegrityBatchService mediaMaintenanceIntegrityBatchService,
    IMediaMaintenanceIntegrityTargetService mediaMaintenanceIntegrityTargetService,
    IMediaMaintenancePrunePathSelectionService mediaMaintenancePrunePathSelectionService
) : IMediaDataMaintenance
{
    public string? Id { get; set; }

    private readonly ILogger<LocalMediaDataMaintenance> _logger = _log;
    private readonly StorageMedia _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IMediaCache _mediaCache = _mediaCache;
    private readonly IMediaDownloadModelMapper _mediaDownloadModelMapper = mediaDownloadModelMapper;
    private readonly IMediaStoragePathService _mediaStoragePathService = mediaStoragePathService;
    private readonly IMediaMaintenanceDownloadProjectionService _mediaMaintenanceDownloadProjectionService =
        mediaMaintenanceDownloadProjectionService;
    private readonly IMediaMaintenanceCachedDownloadFilterService _mediaMaintenanceCachedDownloadFilterService =
        mediaMaintenanceCachedDownloadFilterService;
    private readonly IMediaMaintenanceIntegrityObservationCompositionService _mediaMaintenanceIntegrityObservationCompositionService =
        mediaMaintenanceIntegrityObservationCompositionService;
    private readonly IMediaMaintenanceIntegrityBatchService _mediaMaintenanceIntegrityBatchService =
        mediaMaintenanceIntegrityBatchService;
    private readonly IMediaMaintenanceIntegrityTargetService _mediaMaintenanceIntegrityTargetService =
        mediaMaintenanceIntegrityTargetService;
    private readonly IMediaMaintenancePrunePathSelectionService _mediaMaintenancePrunePathSelectionService =
        mediaMaintenancePrunePathSelectionService;

    public async Task CheckData(List<Download> downloads)
    {
        DeleteTemp();
        await _mediaCache.Load();

        List<MediaDownload> appDownloads = _mediaDownloadModelMapper.ToApplication(downloads);
        Dictionary<string, long?> cacheSizesByPath = appDownloads
            .SelectMany(download => download.Data)
            .Select(item => item.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(path => path, path => _mediaCache.Get(path)?.Size?.File, StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<MediaMaintenanceCachedDownload> cachedDownloads =
            _mediaMaintenanceDownloadProjectionService.ToCachedDownloads(
                appDownloads,
                cacheSizesByPath
            );
        IReadOnlyList<MediaMaintenanceCachedDownload> filtered =
            _mediaMaintenanceCachedDownloadFilterService.Filter(
                cachedDownloads
            );

        downloads.Clear();
        downloads.AddRange(
            _mediaDownloadModelMapper.ToInfrastructure(
                _mediaMaintenanceDownloadProjectionService.ToDownloads(filtered)
            )
        );
        downloads.RemoveAll(dl => dl.Data.Count == 0);
    }

    public async Task CheckIntegrity(List<Download> downloads)
    {
        List<MediaDownload> appDownloads = _mediaDownloadModelMapper.ToApplication(downloads);
        IReadOnlyList<MediaMaintenanceIntegrityTarget> targets =
            _mediaMaintenanceIntegrityTargetService.BuildTargets(appDownloads);
        Dictionary<string, long?> cacheSizesByPath = targets
            .Select(target => target.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(path => path, path => _mediaCache.Get(path)?.Size?.File, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<MediaMaintenanceIntegrityProbeItem> probeItems =
            _mediaMaintenanceIntegrityObservationCompositionService.BuildProbeItems(
                targets,
                cacheSizesByPath
            );
        List<MediaMaintenanceIntegrityProbeOutcome> probeOutcomes = [];

        foreach (MediaMaintenanceIntegrityProbeItem probeItem in probeItems)
        {
            bool isValid = false;

            if (probeItem.ShouldProbe)
            {
                string fullPath = await _mediaCache.GetPath(probeItem.Path);
                isValid = MediaValidator.IsValid(
                    fullPath,
                    () => _logger.LogWarning("path {path} not exist", fullPath)
                );
            }

            probeOutcomes.Add(
                new MediaMaintenanceIntegrityProbeOutcome
                {
                    CorrelationId = probeItem.CorrelationId,
                    CacheFileSize = probeItem.CacheFileSize,
                    IsValidMediaFile = isValid,
                }
            );
        }

        IReadOnlyList<MediaMaintenanceIntegrityObservation> observations =
            _mediaMaintenanceIntegrityObservationCompositionService.ToObservations(probeOutcomes);

        MediaMaintenanceIntegrityBatchResult result = _mediaMaintenanceIntegrityBatchService.Evaluate(
            observations
        );
        HashSet<string> removeSet = result
            .Items.Where(item => item.Remove)
            .Select(item => item.CorrelationId)
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlyList<MediaDownload> filtered = _mediaMaintenanceIntegrityTargetService.RemoveByCorrelations(
            appDownloads,
            removeSet
        );

        downloads.Clear();
        downloads.AddRange(_mediaDownloadModelMapper.ToInfrastructure(filtered));
        MediaMaintenanceIntegritySummary summary = result.Summary;

        _logger.LogInformation(
            "null: {nullCount}, size: {sizeCount}, invalid: {invalidCount}",
            summary.NullCount,
            summary.SizeCount,
            summary.InvalidCount
        );
    }

    public async Task Prune(List<Download> downloads)
    {
        if (!_config.Tasks.Prune)
            return;

        await _mediaCache.Load();

        IReadOnlySet<string> paths = _mediaMaintenancePrunePathSelectionService.SelectPaths(
            _mediaDownloadModelMapper.ToApplication(downloads)
        );

        CancellationTokenSource cts = new();

        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = 64,
            CancellationToken = cts.Token,
        };

        await Parallel.ForEachAsync(
            paths,
            options,
            async (path, ct) =>
            {
                try
                {
                    string fullPath = await _mediaCache.GetPath(path, ct: ct);

                    if (!File.Exists(fullPath))
                        return;

                    File.Delete(fullPath);
                    _logger.LogInformation("media deleted: {path}", fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError("error deleting {media}: {error}", path, ex.Message);
                }
            }
        );
    }

    private string GetPathTemp()
    {
        return _mediaStoragePathService.BuildDownloaderTempPath(
            _partition.GetHeavy().Paths.Select(path => Path.Combine(path)),
            _config.Paths.Tmp.Paths,
            _config.Paths.Tmp.Downloader.Paths
        );
    }

    private void DeleteTemp()
    {
        string path = GetPathTemp();

        if (!Directory.Exists(path))
            return;

        Directory.Delete(path, recursive: true);
        Directory.CreateDirectory(path);
    }

}
