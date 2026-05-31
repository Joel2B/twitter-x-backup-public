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
    IMediaTempPathPolicyService mediaTempPathPolicyService,
    IMediaMaintenanceDownloadProjectionService mediaMaintenanceDownloadProjectionService,
    IMediaMaintenanceCachedDownloadFilterService mediaMaintenanceCachedDownloadFilterService,
    IMediaMaintenanceFileProbePolicyService mediaMaintenanceFileProbePolicyService,
    IMediaMaintenanceIntegrityEvaluationService mediaMaintenanceIntegrityEvaluationService,
    IMediaMaintenanceIntegritySummaryService mediaMaintenanceIntegritySummaryService,
    IMediaMaintenancePrunePathSelectionService mediaMaintenancePrunePathSelectionService
) : IMediaDataMaintenance
{
    public string? Id { get; set; }

    private readonly ILogger<LocalMediaDataMaintenance> _logger = _log;
    private readonly StorageMedia _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IMediaCache _mediaCache = _mediaCache;
    private readonly IMediaTempPathPolicyService _mediaTempPathPolicyService =
        mediaTempPathPolicyService;
    private readonly IMediaMaintenanceDownloadProjectionService _mediaMaintenanceDownloadProjectionService =
        mediaMaintenanceDownloadProjectionService;
    private readonly IMediaMaintenanceCachedDownloadFilterService _mediaMaintenanceCachedDownloadFilterService =
        mediaMaintenanceCachedDownloadFilterService;
    private readonly IMediaMaintenanceFileProbePolicyService _mediaMaintenanceFileProbePolicyService =
        mediaMaintenanceFileProbePolicyService;
    private readonly IMediaMaintenanceIntegrityEvaluationService _mediaMaintenanceIntegrityEvaluationService =
        mediaMaintenanceIntegrityEvaluationService;
    private readonly IMediaMaintenanceIntegritySummaryService _mediaMaintenanceIntegritySummaryService =
        mediaMaintenanceIntegritySummaryService;
    private readonly IMediaMaintenancePrunePathSelectionService _mediaMaintenancePrunePathSelectionService =
        mediaMaintenancePrunePathSelectionService;

    private const long IntegritySizeThreshold = 1000;

    public async Task CheckData(List<Download> downloads)
    {
        DeleteTemp();
        await _mediaCache.Load();

        List<MediaDownload> appDownloads = ToApplication(downloads);
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
        downloads.AddRange(ToInfrastructure(_mediaMaintenanceDownloadProjectionService.ToDownloads(filtered)));
        downloads.RemoveAll(dl => dl.Data.Count == 0);
    }

    public async Task CheckIntegrity(List<Download> downloads)
    {
        List<MediaMaintenanceIntegrityEvaluation> evaluations = [];

        foreach (Download download in downloads)
        {
            for (int i = download.Data.Count - 1; i >= 0; i--)
            {
                DataDownload data = download.Data[i];

                long? size = _mediaCache.Get(data.Path)?.Size?.File;
                string fullPath = string.Empty;
                bool isValid = false;

                if (_mediaMaintenanceFileProbePolicyService.ShouldProbe(size, IntegritySizeThreshold))
                {
                    fullPath = await _mediaCache.GetPath(data.Path);
                    isValid = MediaValidator.IsValid(
                        fullPath,
                        () => _logger.LogWarning("path {path} not exist", fullPath)
                    );
                }

                MediaMaintenanceIntegrityEvaluation evaluation =
                    _mediaMaintenanceIntegrityEvaluationService.Evaluate(
                    size,
                    isValid,
                    IntegritySizeThreshold
                );
                evaluations.Add(evaluation);

                if (evaluation.Remove)
                    download.Data.RemoveAt(i);
            }
        }

        downloads.RemoveAll(dl => dl.Data.Count == 0);
        MediaMaintenanceIntegritySummary summary = _mediaMaintenanceIntegritySummaryService.Summarize(
            evaluations
        );

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
            ToApplication(downloads)
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
        PartitionConfig heavy = _partition.GetHeavy();
        string rootPath = Path.Combine([.. heavy.Paths]);
        return _mediaTempPathPolicyService.BuildDownloaderTempPath(
            rootPath,
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

    private static List<MediaDownload> ToApplication(IEnumerable<Download> downloads) =>
        downloads
            .Select(download => new MediaDownload
            {
                Id = download.Id,
                Data = download
                    .Data.Select(item => new MediaDownloadData { Url = item.Url, Path = item.Path })
                    .ToList(),
            })
            .ToList();

    private static List<Download> ToInfrastructure(IEnumerable<MediaDownload> downloads) =>
        downloads
            .Select(download => new Download
            {
                Id = download.Id,
                Data = download
                    .Data.Select(item => new DataDownload { Url = item.Url, Path = item.Path })
                    .ToList(),
            })
            .ToList();
}
