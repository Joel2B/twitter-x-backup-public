using System.Diagnostics;
using Backup.Application.Core;
using Backup.Application.Media.Maintenance;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Config.Data;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Data;

internal sealed class LocalMediaCacheSnapshotCoordinator(
    IReadOnlyList<MediaCacheTargetRuntime> cacheTargets,
    IPrimarySelectionService primarySelectionService,
    IMediaCacheEntryPathPolicyService mediaCacheEntryPathPolicyService,
    IMediaCacheReplicationPathService mediaCacheReplicationPathService,
    IPartition partition,
    LocalMediaCachePathLayout pathLayout,
    ILogger<LocalMediaCacheSnapshotCoordinator> logger
)
{
    private readonly IReadOnlyList<MediaCacheTargetRuntime> _cacheTargets = cacheTargets;
    private readonly IPrimarySelectionService _primarySelectionService = primarySelectionService;
    private readonly IMediaCacheEntryPathPolicyService _mediaCacheEntryPathPolicyService =
        mediaCacheEntryPathPolicyService;
    private readonly IMediaCacheReplicationPathService _mediaCacheReplicationPathService =
        mediaCacheReplicationPathService;
    private readonly IPartition _partition = partition;
    private readonly LocalMediaCachePathLayout _pathLayout = pathLayout;
    private readonly ILogger<LocalMediaCacheSnapshotCoordinator> _logger = logger;

    public string GetPrimaryFilePath() => GetPrimaryFilePath(GetPrimaryTarget());

    public Task<bool> PrimarySnapshotExists(CancellationToken cancellationToken = default) =>
        GetPrimaryTarget()
            .Persistence.PrimarySnapshotExists(GetPrimaryFilePath(), cancellationToken);

    public async Task LoadIncrementalSnapshotsInto(
        IDictionary<string, MediaCacheEntry> cache,
        CancellationToken cancellationToken = default
    )
    {
        MediaCacheTargetRuntime primaryTarget = GetPrimaryTarget();
        PartitionConfig primary = _partition.GetPrimary();
        string directory = GetIncrementalDirectory(primaryTarget, primary);
        IReadOnlyList<MediaCacheEntry> snapshots =
            await primaryTarget.Persistence.LoadIncrementalSnapshots(directory, cancellationToken);

        foreach (MediaCacheEntry entry in snapshots)
            cache.TryAdd(entry.Path, entry);
    }

    public async Task LoadPrimarySnapshotInto(
        IDictionary<string, MediaCacheEntry> cache,
        CancellationToken cancellationToken = default
    )
    {
        MediaCacheTargetRuntime primaryTarget = GetPrimaryTarget();
        IReadOnlyList<MediaCacheEntry> entries =
            await primaryTarget.Persistence.LoadPrimarySnapshot(
                GetPrimaryFilePath(primaryTarget),
                cancellationToken
            );

        foreach (MediaCacheEntry entry in entries)
            cache[entry.Path] = entry;
    }

    public async Task SavePrimarySnapshot(
        IReadOnlyCollection<MediaCacheEntry> entries,
        CancellationToken cancellationToken = default
    )
    {
        foreach (MediaCacheTargetRuntime target in _cacheTargets)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            string primaryFilePath = GetPrimaryFilePath(target);

            _logger.LogInformation(
                "media cache save started: cache={cacheLabel}, target={targetPath}, entries={entryCount}",
                target.Label,
                primaryFilePath,
                entries.Count
            );

            await target.Persistence.SavePrimarySnapshot(
                primaryFilePath,
                entries,
                cancellationToken
            );

            _logger.LogInformation(
                "media cache save completed: cache={cacheLabel}, target={targetPath}, entries={entryCount}, elapsed={elapsed}",
                target.Label,
                primaryFilePath,
                entries.Count,
                stopwatch.Elapsed
            );
        }
    }

    public async Task ReplicatePrimarySnapshot(CancellationToken cancellationToken = default)
    {
        List<PartitionConfig> partitions = _partition.GetCache();

        foreach (MediaCacheTargetRuntime target in _cacheTargets)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            string primaryFilePath = GetPrimaryFilePath(target);
            List<string> replicaPaths =
            [
                .. _mediaCacheReplicationPathService.GetReplicaPaths(
                    primaryFilePath,
                    partitions.Select(partition => GetCacheFilePath(target, partition))
                ),
            ];

            if (replicaPaths.Count == 0)
            {
                _logger.LogInformation(
                    "media cache replicate skipped: cache={cacheLabel}, source={sourcePath}, replicas=0",
                    target.Label,
                    primaryFilePath
                );
                continue;
            }

            _logger.LogInformation(
                "media cache replicate started: cache={cacheLabel}, source={sourcePath}, replicas={replicaCount}",
                target.Label,
                primaryFilePath,
                replicaPaths.Count
            );

            for (int replicaIndex = 0; replicaIndex < replicaPaths.Count; replicaIndex++)
            {
                string replicaPath = replicaPaths[replicaIndex];

                _logger.LogInformation(
                    "media cache replicate target started: cache={cacheLabel}, replica={replicaIndex}/{replicaCount}, target={targetPath}",
                    target.Label,
                    replicaIndex + 1,
                    replicaPaths.Count,
                    replicaPath
                );

                await target.Persistence.ReplicatePrimarySnapshot(
                    primaryFilePath,
                    [replicaPath],
                    cancellationToken
                );

                _logger.LogInformation(
                    "media cache replicate target completed: cache={cacheLabel}, replica={replicaIndex}/{replicaCount}, target={targetPath}, elapsed={elapsed}",
                    target.Label,
                    replicaIndex + 1,
                    replicaPaths.Count,
                    replicaPath,
                    stopwatch.Elapsed
                );
            }

            _logger.LogInformation(
                "media cache replicate completed: cache={cacheLabel}, replicas={replicaCount}, elapsed={elapsed}",
                target.Label,
                replicaPaths.Count,
                stopwatch.Elapsed
            );
        }
    }

    public async Task SaveIncrementalSnapshot(
        MediaCacheEntry entry,
        CancellationToken cancellationToken = default
    )
    {
        PartitionConfig primary = _partition.GetPrimary();
        string fileName = _mediaCacheEntryPathPolicyService.BuildCacheSnapshotFileName(
            entry.Path,
            entry.PartitionId
        );

        foreach (MediaCacheTargetRuntime target in _cacheTargets)
        {
            await target.Persistence.SaveIncrementalSnapshot(
                GetIncrementalDirectory(target, primary),
                entry,
                fileName,
                cancellationToken
            );
        }
    }

    public void ResetIncrementalSnapshots()
    {
        PartitionConfig primary = _partition.GetPrimary();

        foreach (MediaCacheTargetRuntime target in _cacheTargets)
            target.Persistence.ResetIncrementalSnapshotDirectory(
                GetIncrementalDirectory(target, primary)
            );
    }

    private MediaCacheTargetRuntime GetPrimaryTarget() =>
        _primarySelectionService.ResolvePrimary(
            _cacheTargets,
            target => target.IsDefault,
            "No enabled media caches are configured.",
            "Only one enabled media cache can be marked as default."
        );

    private string GetPrimaryFilePath(MediaCacheTargetRuntime target)
    {
        PartitionConfig primary = _partition.GetPrimary();
        return GetCacheFilePath(target, primary);
    }

    private string GetCacheFilePath(MediaCacheTargetRuntime target, PartitionConfig partition) =>
        target.Path is not null
            ? _pathLayout.GetCacheFilePath(partition, target.Path)
            : _pathLayout.GetVirtualPrimaryCacheFilePath(partition, target.Key);

    private string GetIncrementalDirectory(
        MediaCacheTargetRuntime target,
        PartitionConfig partition
    ) => _pathLayout.GetIncrementalCacheDirectory(partition, target.Key);
}
