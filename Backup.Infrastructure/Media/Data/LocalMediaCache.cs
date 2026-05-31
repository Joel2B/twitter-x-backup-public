using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Backup.Application.IO;
using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Media.Data;

public class LocalMediaCache(
    ILogger<LocalMediaCache> _logger,
    StorageMedia _config,
    IPartition _partition,
    IDataStoreGuardService dataStoreGuardService,
    IMediaCacheDirectoryPolicyService mediaCacheDirectoryPolicyService,
    IMediaCacheRecheckPolicyService mediaCacheRecheckPolicyService,
    IMediaCacheRecheckOrchestrationService mediaCacheRecheckOrchestrationService,
    IMediaCachePartitionSizeAggregationService mediaCachePartitionSizeAggregationService,
    IMediaCacheReplicationPathService mediaCacheReplicationPathService
) : IMediaCache
{
    private readonly ILogger<LocalMediaCache> _logger = _logger;
    private readonly StorageMedia _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly IMediaCacheDirectoryPolicyService _mediaCacheDirectoryPolicyService =
        mediaCacheDirectoryPolicyService;
    private readonly IMediaCacheRecheckPolicyService _mediaCacheRecheckPolicyService =
        mediaCacheRecheckPolicyService;
    private readonly IMediaCacheRecheckOrchestrationService _mediaCacheRecheckOrchestrationService =
        mediaCacheRecheckOrchestrationService;
    private readonly IMediaCachePartitionSizeAggregationService _mediaCachePartitionSizeAggregationService =
        mediaCachePartitionSizeAggregationService;
    private readonly IMediaCacheReplicationPathService _mediaCacheReplicationPathService =
        mediaCacheReplicationPathService;

    private readonly ConcurrentDictionary<string, MediaCacheEntry> _cache = new(
        StringComparer.OrdinalIgnoreCase
    );

    public Task Setup()
    {
        SetupDirectory();

        return Task.CompletedTask;
    }

    private void SetupDirectory()
    {
        foreach (PartitionConfig partition in _partition.GetPartitions())
        {
            if (
                _mediaCacheDirectoryPolicyService.ShouldCreateCacheDirectory(
                    partition.Type,
                    partition.Tags
                )
            )
                Directory.CreateDirectory(GetPathCache(partition));

            if (_mediaCacheDirectoryPolicyService.ShouldCreateMediaDirectory(partition.Type))
                Directory.CreateDirectory(GetPathMedia(partition));
        }

        Directory.CreateDirectory(GetPathCacheDownload(_partition.GetPrimary()));
    }

    public string GetPathCacheDownload(PartitionConfig partition) =>
        Path.Combine(
            [.. partition.Paths, .. _config.Paths.Tmp.Paths, .. _config.Paths.Tmp.Downloaded.Paths]
        );

    public string GetPathMedia(PartitionConfig partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Media.Paths]);

    public string GetPathCache(PartitionConfig partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Cache.Paths]);

    public string GetPathCacheFile(PartitionConfig partition)
    {
        string fileName = _dataStoreGuardService.RequireConfiguredFileName(_config.Paths.Cache.File);

        return Path.Combine(GetPathCache(partition), fileName);
    }

    public string GetPathCacheFilePrimary() => GetPathCacheFile(_partition.GetPrimary());

    public async Task Load()
    {
        await Replicate();

        string file = GetPathCacheFilePrimary();
        IReadOnlyCollection<string> recheck = [];

        if (File.Exists(file))
        {
            if (_cache.IsEmpty)
            {
                await LoadCache();

                await foreach (MediaCacheEntry entry in LocalMediaCacheReader.Get(file))
                    _cache[entry.Path] = entry;
            }

            _logger.LogWarning("cache: {count}", _cache.Count);
            List<MediaCacheRecheckCandidate> candidates = _cache
                .Values.Select(entry => new MediaCacheRecheckCandidate
                {
                    Path = entry.Path,
                    StreamSizeBytes = entry.Size?.Stream,
                    FileSizeBytes = entry.Size?.File,
                })
                .ToList();
            recheck = _mediaCacheRecheckOrchestrationService.SelectRecheckPaths(candidates);
        }
        else
            _logger.LogWarning("cache file not exist, {path}", file);

        _logger.LogWarning("recheck: {count}", recheck.Count);

        ParallelOptions options = new() { MaxDegreeOfParallelism = 1 };

        Parallel.ForEach(
            recheck,
            options,
            path =>
                {
                    try
                    {
                        MediaCacheEntry? cache = Get(path);

                        if (cache is null)
                            throw new Exception();

                        int? partitionId = cache.PartitionId;
                        long? streamSize = cache.Size?.Stream;
                        bool fileExists = false;
                        long? fileSize = null;

                        if (partitionId is not null)
                        {
                            PartitionConfig partition = _partition.GetPath(partitionId);
                            string fullPath = Path.Combine(
                                [GetPathMedia(partition), UtilsPath.NormalizePath(path)]
                            );
                            FileInfo fi = new(fullPath);
                            fileExists = fi.Exists;
                            fileSize = fileExists ? fi.Length : null;
                        }

                        MediaCacheRecheckResult decision = _mediaCacheRecheckOrchestrationService.Evaluate(
                            new MediaCacheRecheckObservation
                            {
                                Path = path,
                                PartitionId = partitionId,
                                StreamSizeBytes = streamSize,
                                FileExists = fileExists,
                                FileSizeBytes = fileSize,
                            }
                        );

                        if (decision.IsInvalid)
                            throw new Exception();

                        if (decision.ShouldRemove)
                        {
                            bool removed = _cache.TryRemove(path, out var _);

                        if (removed)
                            _logger.LogWarning("{path} path removed from cache", path);
                        else
                            _logger.LogError("error removing path {path}", path);

                            return;
                        }

                        if (!decision.ShouldUpdate)
                            return;

                        MediaCacheEntry newCache = new()
                        {
                            Path = path,
                            PartitionId = decision.PartitionId,
                            Size = new()
                            {
                                Stream = decision.StreamSizeBytes,
                                File = decision.FileSizeBytes,
                            },
                        };

                        _cache.TryUpdate(path, newCache, cache);
                    }
                catch (Exception ex)
                {
                    _logger.LogError("Error in {path}: {error}", path, ex.Message);
                }
            }
        );

        IReadOnlyDictionary<int, long> sizes = _mediaCachePartitionSizeAggregationService.Aggregate(
            _cache.Values.Select(entry =>
                new KeyValuePair<int?, long?>(entry.PartitionId, entry.Size?.File)
            )
        );

        _partition.SetupSizes(sizes.ToDictionary(item => item.Key, item => item.Value));

        if (recheck.Count > 0)
        {
            await LocalMediaCacheReader.Save(file, [.. _cache.Values]);
            DeleteCache();
        }
    }

    private async Task LoadCache()
    {
        PartitionConfig primary = _partition.GetPrimary();
        string directory = GetPathCacheDownload(primary);

        if (!Directory.Exists(directory))
            return;

        foreach (
            string path in Directory.EnumerateFiles(
                directory,
                "*.cache",
                SearchOption.TopDirectoryOnly
            )
        )
        {
            string json = await File.ReadAllTextAsync(path);
            MediaCacheEntry? cache = JsonConvert.DeserializeObject<MediaCacheEntry>(json);

            if (cache is null)
                continue;

            _cache.TryAdd(cache.Path, cache);
        }
    }

    private async Task Replicate()
    {
        string primaryFilePath = GetPathCacheFilePrimary();

        if (!File.Exists(primaryFilePath))
            return;

        List<PartitionConfig> partitions = _partition.GetCache();
        IReadOnlyList<string> replicaPaths = _mediaCacheReplicationPathService.GetReplicaPaths(
            primaryFilePath,
            partitions.Select(GetPathCacheFile)
        );

        foreach (string path in replicaPaths)
        {
            if (File.Exists(path))
                File.Delete(path);

            File.Copy(primaryFilePath, path);
        }
    }

    private async Task SaveCache(MediaCacheEntry cache, CancellationToken ct)
    {
        PartitionConfig primary = _partition.GetPrimary();
        string pathCache = GetPathCacheDownload(primary);
        string name = $"{cache.Path}{cache.PartitionId}";

        byte[] data = Encoding.UTF8.GetBytes(name);
        byte[] bytes = SHA256.HashData(data);

        string hash = Convert.ToHexString(bytes).ToLower();
        string fileName = $"{hash}.cache";
        string path = Path.Combine(pathCache, fileName);

        string json = JsonConvert.SerializeObject(cache);

        using FileStream fs = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough
        );

        using StreamWriter sw = new(fs);

        await sw.WriteAsync(json.AsMemory(), ct);
        await sw.FlushAsync(ct);
        fs.Flush(true);
    }

    private void DeleteCache()
    {
        PartitionConfig primary = _partition.GetPrimary();
        string path = GetPathCacheDownload(primary);

        if (!Directory.Exists(path))
            return;

        Directory.Delete(path, recursive: true);
        Directory.CreateDirectory(path);
    }

    public async Task<string> GetPath(string path, long size = 0, CancellationToken ct = default)
    {
        MediaCacheEntry? cache = Get(path);
        PartitionConfig? partition = null;

        if (size > 0 && size >= _config.SizeHeavy)
            partition = _partition.GetHeavy();

        partition ??= _partition.GetPath(cache?.PartitionId, size);

        if (size > 0)
        {
            string normalizedPath = UtilsPath.NormalizePath(path, true);

            MediaCacheEntry newCache = new()
            {
                Path = normalizedPath,
                PartitionId = partition.Id,
                Size = new() { Stream = size },
            };

            _cache.AddOrUpdate(
                normalizedPath,
                _ => newCache,
                (_, old) =>
                {
                    if (old.Size?.Stream != newCache.Size.Stream)
                        throw new Exception("different sizes");

                    return old;
                }
            );

            await SaveCache(newCache, ct);
        }

        return Path.Combine([GetPathMedia(partition), path]);
    }

    public MediaCacheEntry? Get(string path)
    {
        path = UtilsPath.NormalizePath(path, true);
        _cache.TryGetValue(path, out MediaCacheEntry? cache);

        return cache;
    }
}
