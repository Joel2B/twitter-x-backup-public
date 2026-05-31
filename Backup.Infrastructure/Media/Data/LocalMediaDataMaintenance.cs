using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Models;
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
    IMediaMaintenanceDataPolicyService mediaMaintenanceDataPolicyService,
    IMediaMaintenanceIntegrityEvaluationService mediaMaintenanceIntegrityEvaluationService,
    IMediaMaintenancePrunePathSelectionService mediaMaintenancePrunePathSelectionService
) : IMediaDataMaintenance
{
    public string? Id { get; set; }

    private readonly ILogger<LocalMediaDataMaintenance> _logger = _log;
    private readonly StorageMedia _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IMediaCache _mediaCache = _mediaCache;
    private readonly IMediaMaintenanceDataPolicyService _mediaMaintenanceDataPolicyService =
        mediaMaintenanceDataPolicyService;
    private readonly IMediaMaintenanceIntegrityEvaluationService _mediaMaintenanceIntegrityEvaluationService =
        mediaMaintenanceIntegrityEvaluationService;
    private readonly IMediaMaintenancePrunePathSelectionService _mediaMaintenancePrunePathSelectionService =
        mediaMaintenancePrunePathSelectionService;

    private const long IntegritySizeThreshold = 1000;

    public async Task CheckData(List<Download> downloads)
    {
        DeleteTemp();
        await _mediaCache.Load();

        foreach (Download download in downloads)
        {
            download.Data.RemoveAll(data =>
            {
                long? cacheFileSize = _mediaCache.Get(data.Path)?.Size?.File;
                return _mediaMaintenanceDataPolicyService.ShouldRemoveCachedDownload(cacheFileSize);
            });
        }

        downloads.RemoveAll(dl => dl.Data.Count == 0);
    }

    public async Task CheckIntegrity(List<Download> downloads)
    {
        int nullCount = 0;
        int sizeCount = 0;
        int invalidCount = 0;

        foreach (Download download in downloads)
        {
            for (int i = download.Data.Count - 1; i >= 0; i--)
            {
                DataDownload data = download.Data[i];

                long? size = _mediaCache.Get(data.Path)?.Size?.File;
                string fullPath = string.Empty;
                bool isValid = false;

                if (size is not null)
                {
                    if (size < IntegritySizeThreshold)
                    {
                        fullPath = await _mediaCache.GetPath(data.Path);
                        isValid = MediaValidator.IsValid(
                            fullPath,
                            () => _logger.LogWarning("path {path} not exist", fullPath)
                        );
                    }
                }

                var evaluation = _mediaMaintenanceIntegrityEvaluationService.Evaluate(
                    size,
                    isValid,
                    IntegritySizeThreshold
                );

                nullCount += evaluation.NullCountIncrement;
                sizeCount += evaluation.SizeCountIncrement;
                invalidCount += evaluation.InvalidCountIncrement;

                if (evaluation.Remove)
                    download.Data.RemoveAt(i);
            }
        }

        downloads.RemoveAll(dl => dl.Data.Count == 0);

        _logger.LogInformation(
            "null: {nullCount}, size: {sizeCount}, invalid: {invalidCount}",
            nullCount,
            sizeCount,
            invalidCount
        );
    }

    public async Task Prune(List<Download> downloads)
    {
        if (!_config.Tasks.Prune)
            return;

        await _mediaCache.Load();

        IReadOnlySet<string> paths = _mediaMaintenancePrunePathSelectionService.SelectPaths(
            downloads.Select(download => new MediaDownload
            {
                Id = download.Id,
                Data = download
                    .Data.Select(item => new MediaDownloadData { Url = item.Url, Path = item.Path })
                    .ToList(),
            })
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

        return Path.Combine(
            [.. heavy.Paths, .. _config.Paths.Tmp.Paths, .. _config.Paths.Tmp.Downloader.Paths]
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
