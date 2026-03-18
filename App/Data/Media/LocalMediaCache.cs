using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Media;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.App.Data.Media;

public class LocalMediaCache(
    ILogger<LocalMediaCache> _logger,
    Models.Config.Data.Media.Storage _config,
    IPartition _partition
) : ISetup
{
    private readonly ILogger<LocalMediaCache> _logger = _logger;
    private readonly Models.Config.Data.Media.Storage _config = _config;
    private readonly IPartition _partition = _partition;

    private readonly ConcurrentDictionary<string, Cache> _cache = new(
        StringComparer.OrdinalIgnoreCase
    );

    public Task Setup()
    {
        SetupDirectory();

        return Task.CompletedTask;
    }

    private void SetupDirectory()
    {
        foreach (Models.Config.Data.Partition partition in _partition.GetPartitions())
        {
            if (
                partition.Type == "primary"
                || partition.Type == "cache"
                || (partition.Tags is not null && partition.Tags.Contains("cache"))
            )
                Directory.CreateDirectory(GetPathCache(partition));

            if (
                partition.Type == "primary"
                || partition.Type == "extension"
                || partition.Type == "heavy"
            )
                Directory.CreateDirectory(GetPathMedia(partition));
        }

        Directory.CreateDirectory(GetPathCacheDownload(_partition.GetPrimary()));
    }

    public string GetPathCacheDownload(Models.Config.Data.Partition partition) =>
        Path.Combine(
            [.. partition.Paths, .. _config.Paths.Tmp.Paths, .. _config.Paths.Tmp.Downloaded.Paths]
        );

    public string GetPathMedia(Models.Config.Data.Partition partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Media.Paths]);

    public string GetPathCache(Models.Config.Data.Partition partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Cache.Paths]);

    public string GetPathCacheFile(Models.Config.Data.Partition partition)
    {
        if (_config.Paths.Cache.File is null)
            throw new Exception("file not configured");

        return Path.Combine(GetPathCache(partition), _config.Paths.Cache.File);
    }

    public string GetPathCacheFilePrimary() => GetPathCacheFile(_partition.GetPrimary());

    public async Task Load()
    {
        await Replicate();

        string file = GetPathCacheFilePrimary();
        HashSet<string> recheck = new(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(file))
        {
            if (_cache.IsEmpty)
            {
                await LoadCache();

                await foreach (Cache entry in LocalMediaCacheReader.Get(file))
                    _cache[entry.Path] = entry;
            }

            _logger.LogWarning("cache: {count}", _cache.Count);

            foreach (Cache entry in _cache.Values)
            {
                if (
                    entry.Size?.Stream is null
                    || (entry.Size.File is not null && entry.Size.File != 0)
                )
                    continue;

                recheck.Add(entry.Path);
            }
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
                    Cache? cache = Get(path);

                    if (cache is null || cache.Size is null)
                        throw new Exception();

                    Models.Config.Data.Partition partition = _partition.GetPath(cache.PartitionId);

                    string fullPath = Path.Combine(
                        [GetPathMedia(partition), Utils.Path.NormalizePath(path)]
                    );

                    FileInfo fi = new(fullPath);

                    if (!fi.Exists)
                    {
                        bool removed = _cache.TryRemove(path, out var _);

                        if (removed)
                            _logger.LogWarning("{path} path removed from cache", path);
                        else
                            _logger.LogError("error removing path {path}", path);

                        return;
                    }

                    Cache newCache = new()
                    {
                        Path = path,
                        PartitionId = cache.PartitionId,
                        Size = new() { Stream = cache.Size.Stream, File = fi.Length },
                    };

                    _cache.TryUpdate(path, newCache, cache);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error in {path}: {error}", path, ex.Message);
                }
            }
        );

        Dictionary<int, long> sizes = _cache
            .GroupBy(o => o.Value.PartitionId ?? -1)
            .ToDictionary(o => o.Key, o => o.Sum(o => o.Value.Size?.File ?? 0));

        _partition.SetupSizes(sizes);

        if (recheck.Count > 0)
        {
            await LocalMediaCacheReader.Save(file, [.. _cache.Values]);
            DeleteCache();
        }
    }

    private async Task LoadCache()
    {
        Models.Config.Data.Partition primary = _partition.GetPrimary();
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
            Cache? cache = JsonConvert.DeserializeObject<Cache>(json);

            if (cache is null)
                continue;

            _cache.TryAdd(cache.Path, cache);
        }
    }

    private async Task Replicate()
    {
        string file = GetPathCacheFilePrimary();

        if (!File.Exists(file))
            return;

        List<Models.Config.Data.Partition> partitions = _partition.GetCache();

        foreach (Models.Config.Data.Partition partition in partitions)
        {
            string path = GetPathCacheFile(partition);

            File.Delete(path);
            File.Copy(file, path);
        }
    }

    private async Task SaveCache(Cache cache, CancellationToken ct)
    {
        Models.Config.Data.Partition primary = _partition.GetPrimary();
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
        Models.Config.Data.Partition primary = _partition.GetPrimary();
        string path = GetPathCacheDownload(primary);

        if (!Directory.Exists(path))
            return;

        Directory.Delete(path, recursive: true);
        Directory.CreateDirectory(path);
    }

    public async Task<string> GetPath(string path, long size = 0, CancellationToken ct = default)
    {
        Cache? cache = Get(path);
        Models.Config.Data.Partition? partition = null;

        if (size > 0 && size >= _config.SizeHeavy)
            partition = _partition.GetHeavy();

        partition ??= _partition.GetPath(cache?.PartitionId, size);

        if (size > 0)
        {
            string normalizedPath = Utils.Path.NormalizePath(path, true);

            Cache newCache = new()
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

    public Cache? Get(string path)
    {
        path = Utils.Path.NormalizePath(path, true);
        _cache.TryGetValue(path, out Cache? cache);

        return cache;
    }
}
