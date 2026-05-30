using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Media.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Data;

public class RedisMediaCache(
    ILogger<RedisMediaCache> _logger,
    StorageMedia _storage,
    IPartition _partition
) : IMediaCache
{
    private readonly ILogger<RedisMediaCache> _logger = _logger;
    private readonly StorageMedia _storage = _storage;
    private readonly IPartition _partition = _partition;

    public Task Setup()
    {
        _logger.LogWarning(
            "media cache backend '{backend}' selected with {partitions} partitions, but implementation is pending",
            "redis",
            _partition.GetPartitions().Count
        );

        return Task.CompletedTask;
    }

    public Task Load() => ThrowNotImplemented<Task>();

    public Task<string> GetPath(string path, long size = 0, CancellationToken ct = default) =>
        ThrowNotImplemented<Task<string>>();

    public MediaCacheEntry? Get(string path) => ThrowNotImplemented<MediaCacheEntry?>();

    private T ThrowNotImplemented<T>()
    {
        string? connection = _storage.CacheBackend?.ConnectionString;
        throw new NotSupportedException(
            $"Redis media cache backend is not implemented yet. Connection='{connection ?? "(null)"}'."
        );
    }
}
