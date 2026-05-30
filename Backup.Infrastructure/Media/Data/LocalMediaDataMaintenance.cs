using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Interfaces.Partition;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Models.Media;
using Backup.Infrastructure.Models.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Data.Media;

public class LocalMediaDataMaintenance(
    ILogger<LocalMediaDataMaintenance> _log,
    StorageMedia _config,
    IPartition _partition,
    IMediaCache _mediaCache
) : IMediaDataMaintenance
{
    public string? Id { get; set; }

    private readonly ILogger<LocalMediaDataMaintenance> _logger = _log;
    private readonly StorageMedia _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IMediaCache _mediaCache = _mediaCache;

    public async Task CheckData(List<Download> downloads)
    {
        DeleteTemp();
        await _mediaCache.Load();

        foreach (Download download in downloads)
        {
            download.Data.RemoveAll(data =>
            {
                MediaCacheEntry? cache = _mediaCache.Get(data.Path);

                return cache is not null && cache.Size?.File is long sz && sz != 0;
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
                string fullPath = "";

                if (size is not null)
                    fullPath = await _mediaCache.GetPath(data.Path);

                bool remove = false;

                if (size is null)
                {
                    remove = true;
                    nullCount++;
                }

                if (size >= 1000)
                    remove = true;
                else
                    sizeCount++;

                if (!remove)
                {
                    if (
                        MediaValidator.IsValid(
                            fullPath,
                            () => _logger.LogWarning("path {path} not exist", fullPath)
                        )
                    )
                    {
                        remove = true;
                    }
                    else
                    {
                        invalidCount++;
                    }
                }

                if (remove)
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

        HashSet<string> paths = downloads
            .SelectMany(download => download.Data.Select(o => o.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
