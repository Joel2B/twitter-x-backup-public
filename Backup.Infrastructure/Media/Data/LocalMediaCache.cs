using System.Collections.Concurrent;
using Backup.Application.Media.Maintenance;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Data;

// Facade for media cache IO and reconciliation flows. It coordinates cache
// collaborators but preserves the existing cache file and path behavior.
public class LocalMediaCache : IMediaCache
{
    private readonly ConcurrentDictionary<string, MediaCacheEntry> _cache = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly IMediaCacheEntryPathPolicyService _mediaCacheEntryPathPolicyService;
    private readonly LocalMediaCachePathLayout _pathLayout;
    private readonly LocalMediaCacheSnapshotCoordinator _snapshotCoordinator;
    private readonly LocalMediaCacheMutationApplier _mutationApplier;
    private readonly LocalMediaCacheLoadCoordinator _loadCoordinator;
    private readonly LocalMediaCacheWriteCoordinator _writeCoordinator;

    internal LocalMediaCache(
        IMediaCacheEntryPathPolicyService mediaCacheEntryPathPolicyService,
        LocalMediaCachePathLayout pathLayout,
        LocalMediaCacheSnapshotCoordinator snapshotCoordinator,
        LocalMediaCacheMutationApplier mutationApplier,
        LocalMediaCacheLoadCoordinator loadCoordinator,
        LocalMediaCacheWriteCoordinator writeCoordinator
    )
    {
        _mediaCacheEntryPathPolicyService = mediaCacheEntryPathPolicyService;
        _pathLayout = pathLayout;
        _snapshotCoordinator = snapshotCoordinator;
        _mutationApplier = mutationApplier;
        _loadCoordinator = loadCoordinator;
        _writeCoordinator = writeCoordinator;
    }

    public Task Setup()
    {
        _pathLayout.EnsureDirectories();

        return Task.CompletedTask;
    }

    public async Task Load() => await _loadCoordinator.Load(_cache);

    public async Task<string> GetPath(string path, long size = 0, CancellationToken ct = default)
    {
        MediaCacheEntry? cache = Get(path);
        return await _writeCoordinator.GetPath(_cache, cache, path, size, ct);
    }

    public MediaCacheEntry? Get(string path)
    {
        path = _mediaCacheEntryPathPolicyService.NormalizeForCacheKey(path);
        _cache.TryGetValue(path, out MediaCacheEntry? cache);

        return cache;
    }
}
