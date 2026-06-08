using Backup.Application.Media;
using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;
using Backup.Application.Media.Models;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Config.Data.Media;
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

    public Task<int> GetCacheCount(CancellationToken cancellationToken = default) =>
        Task.FromResult(_mediaCache.Count);

    public async Task CheckData(
        List<Download> downloads,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "check-data: starting with {DownloadCount} downloads",
            downloads.Count
        );

        DeleteTemp();

        _logger.LogInformation("check-data: loading media cache");
        await _mediaCache.Load();
        _logger.LogInformation("check-data: media cache loaded");

        List<MediaDownload> appDownloads = _mediaDownloadModelMapper.ToApplication(downloads);
        _logger.LogInformation(
            "check-data: projected {DownloadCount} application downloads",
            appDownloads.Count
        );

        Dictionary<string, long?> cacheSizesByPath = appDownloads
            .SelectMany(download => download.Data)
            .Select(item => item.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                path => path,
                path => _mediaCache.Get(path)?.Size?.File,
                StringComparer.OrdinalIgnoreCase
            );

        _logger.LogInformation(
            "check-data: mapped cache sizes for {PathCount} distinct paths",
            cacheSizesByPath.Count
        );

        IReadOnlyList<MediaMaintenanceCachedDownload> cachedDownloads =
            _mediaMaintenanceDownloadProjectionService.ToCachedDownloads(
                appDownloads,
                cacheSizesByPath
            );

        _logger.LogInformation(
            "check-data: built {DownloadCount} cached download candidates",
            cachedDownloads.Count
        );

        IReadOnlyList<MediaMaintenanceCachedDownload> filtered =
            _mediaMaintenanceCachedDownloadFilterService.Filter(cachedDownloads);

        _logger.LogInformation(
            "check-data: filter kept {DownloadCount} cached downloads",
            filtered.Count
        );

        downloads.Clear();
        downloads.AddRange(
            _mediaDownloadModelMapper.ToInfrastructure(
                _mediaMaintenanceDownloadProjectionService.ToDownloads(filtered)
            )
        );

        downloads.RemoveAll(dl => dl.Data.Count == 0);

        _logger.LogInformation(
            "check-data: completed with {DownloadCount} downloads",
            downloads.Count
        );
    }

    public async Task CheckIntegrity(
        List<Download> downloads,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<MediaDownload> appDownloads = _mediaDownloadModelMapper.ToApplication(downloads);
        IReadOnlyList<MediaMaintenanceIntegrityTarget> targets =
            _mediaMaintenanceIntegrityTargetService.BuildTargets(appDownloads);
        Dictionary<string, long?> cacheSizesByPath = targets
            .Select(target => target.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                path => path,
                path => _mediaCache.Get(path)?.Size?.File,
                StringComparer.OrdinalIgnoreCase
            );
        IReadOnlyList<MediaMaintenanceIntegrityProbeItem> probeItems =
            _mediaMaintenanceIntegrityObservationCompositionService.BuildProbeItems(
                targets,
                cacheSizesByPath
            );
        List<MediaMaintenanceIntegrityProbeOutcome> probeOutcomes = [];

        foreach (MediaMaintenanceIntegrityProbeItem probeItem in probeItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

        MediaMaintenanceIntegrityBatchResult result =
            _mediaMaintenanceIntegrityBatchService.Evaluate(observations);
        HashSet<string> removeSet = result
            .Items.Where(item => item.Remove)
            .Select(item => item.CorrelationId)
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlyList<MediaDownload> filtered =
            _mediaMaintenanceIntegrityTargetService.RemoveByCorrelations(appDownloads, removeSet);

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

    public async Task Prune(List<Download> downloads, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_config.Tasks.Prune)
            return;

        await _mediaCache.Load();

        IReadOnlySet<string> paths = _mediaMaintenancePrunePathSelectionService.SelectPaths(
            _mediaDownloadModelMapper.ToApplication(downloads)
        );

        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = 64,
            CancellationToken = cancellationToken,
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
                catch (OperationCanceledException)
                {
                    throw;
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
