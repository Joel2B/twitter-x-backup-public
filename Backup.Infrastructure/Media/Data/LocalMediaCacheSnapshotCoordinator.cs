using Backup.Application.Media.Maintenance;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Infrastructure.Media.Data;

internal sealed class LocalMediaCacheSnapshotCoordinator(
    IPartition partition,
    IMediaCachePersistenceIOService mediaCachePersistenceIOService,
    IMediaCacheEntryPathPolicyService mediaCacheEntryPathPolicyService,
    IMediaCacheReplicationPathService mediaCacheReplicationPathService,
    Func<PartitionConfig, string> getPathCacheDownload,
    Func<PartitionConfig, string> getPathCacheFile
)
{
    private readonly IPartition _partition = partition;
    private readonly IMediaCachePersistenceIOService _mediaCachePersistenceIOService =
        mediaCachePersistenceIOService;
    private readonly IMediaCacheEntryPathPolicyService _mediaCacheEntryPathPolicyService =
        mediaCacheEntryPathPolicyService;
    private readonly IMediaCacheReplicationPathService _mediaCacheReplicationPathService =
        mediaCacheReplicationPathService;
    private readonly Func<PartitionConfig, string> _getPathCacheDownload = getPathCacheDownload;
    private readonly Func<PartitionConfig, string> _getPathCacheFile = getPathCacheFile;

    public string GetPrimaryFilePath() => _getPathCacheFile(_partition.GetPrimary());

    public async Task LoadIncrementalSnapshotsInto(
        IDictionary<string, MediaCacheEntry> cache,
        CancellationToken cancellationToken = default
    )
    {
        PartitionConfig primary = _partition.GetPrimary();
        string directory = _getPathCacheDownload(primary);
        IReadOnlyList<MediaCacheEntry> snapshots =
            await _mediaCachePersistenceIOService.LoadIncrementalSnapshots(
                directory,
                cancellationToken
            );

        foreach (MediaCacheEntry entry in snapshots)
            cache.TryAdd(entry.Path, entry);
    }

    public async Task LoadPrimarySnapshotInto(
        IDictionary<string, MediaCacheEntry> cache,
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyList<MediaCacheEntry> entries =
            await _mediaCachePersistenceIOService.LoadPrimarySnapshot(
                GetPrimaryFilePath(),
                cancellationToken
            );

        foreach (MediaCacheEntry entry in entries)
            cache[entry.Path] = entry;
    }

    public Task SavePrimarySnapshot(
        IReadOnlyCollection<MediaCacheEntry> entries,
        CancellationToken cancellationToken = default
    ) =>
        _mediaCachePersistenceIOService.SavePrimarySnapshot(
            GetPrimaryFilePath(),
            entries,
            cancellationToken
        );

    public Task ReplicatePrimarySnapshot(CancellationToken cancellationToken = default)
    {
        string primaryFilePath = GetPrimaryFilePath();
        List<PartitionConfig> partitions = _partition.GetCache();
        IReadOnlyList<string> replicaPaths = _mediaCacheReplicationPathService.GetReplicaPaths(
            primaryFilePath,
            partitions.Select(_getPathCacheFile)
        );

        return _mediaCachePersistenceIOService.ReplicatePrimarySnapshot(
            primaryFilePath,
            replicaPaths,
            cancellationToken
        );
    }

    public Task SaveIncrementalSnapshot(
        MediaCacheEntry entry,
        CancellationToken cancellationToken = default
    )
    {
        PartitionConfig primary = _partition.GetPrimary();
        string directory = _getPathCacheDownload(primary);
        string fileName = _mediaCacheEntryPathPolicyService.BuildCacheSnapshotFileName(
            entry.Path,
            entry.PartitionId
        );

        return _mediaCachePersistenceIOService.SaveIncrementalSnapshot(
            directory,
            entry,
            fileName,
            cancellationToken
        );
    }

    public void ResetIncrementalSnapshots()
    {
        PartitionConfig primary = _partition.GetPrimary();
        string directory = _getPathCacheDownload(primary);
        _mediaCachePersistenceIOService.ResetIncrementalSnapshotDirectory(directory);
    }
}
