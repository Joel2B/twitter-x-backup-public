using System.Collections.Concurrent;
using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Config.Data;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Data;

internal sealed class LocalMediaCacheLoadCoordinator(
    ILogger logger,
    IPartition partition,
    IMediaCacheLoadExecutionService mediaCacheLoadExecutionService,
    IMediaCacheRecheckProbeExecutionService mediaCacheRecheckProbeExecutionService,
    IMediaCachePartitionSizeAggregationService mediaCachePartitionSizeAggregationService,
    IMediaCacheEntryPathPolicyService mediaCacheEntryPathPolicyService,
    LocalMediaCachePathLayout pathLayout,
    LocalMediaCacheSnapshotCoordinator snapshotCoordinator,
    LocalMediaCacheMutationApplier mutationApplier
)
{
    private readonly ILogger _logger = logger;
    private readonly IPartition _partition = partition;
    private readonly IMediaCacheLoadExecutionService _mediaCacheLoadExecutionService =
        mediaCacheLoadExecutionService;
    private readonly IMediaCacheRecheckProbeExecutionService _mediaCacheRecheckProbeExecutionService =
        mediaCacheRecheckProbeExecutionService;
    private readonly IMediaCachePartitionSizeAggregationService _mediaCachePartitionSizeAggregationService =
        mediaCachePartitionSizeAggregationService;
    private readonly IMediaCacheEntryPathPolicyService _mediaCacheEntryPathPolicyService =
        mediaCacheEntryPathPolicyService;
    private readonly LocalMediaCachePathLayout _pathLayout = pathLayout;
    private readonly LocalMediaCacheSnapshotCoordinator _snapshotCoordinator = snapshotCoordinator;
    private readonly LocalMediaCacheMutationApplier _mutationApplier = mutationApplier;

    public async Task Load(
        ConcurrentDictionary<string, MediaCacheEntry> cache,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("cache-load: starting");
        _logger.LogInformation("cache-load: replicating primary snapshot");
        await _snapshotCoordinator.ReplicatePrimarySnapshot(cancellationToken);
        _logger.LogInformation("cache-load: primary snapshot replication completed");

        string file = _snapshotCoordinator.GetPrimaryFilePath();
        bool primarySnapshotExists = await _snapshotCoordinator.PrimarySnapshotExists(
            cancellationToken
        );

        if (primarySnapshotExists)
        {
            if (cache.IsEmpty)
            {
                _logger.LogInformation("cache-load: loading primary snapshot");
                await _snapshotCoordinator.LoadPrimarySnapshotInto(cache, cancellationToken);
                _logger.LogInformation(
                    "cache-load: primary snapshot loaded, cache count {Count}",
                    cache.Count
                );

                _logger.LogInformation("cache-load: loading incremental snapshots");
                await _snapshotCoordinator.LoadIncrementalSnapshotsInto(cache, cancellationToken);
                _logger.LogInformation(
                    "cache-load: incremental snapshots loaded, cache count {Count}",
                    cache.Count
                );
            }

            _logger.LogWarning("cache: {count}", cache.Count);
        }
        else
            _logger.LogWarning("cache primary snapshot not found, locator {path}", file);

        IReadOnlyList<MediaCacheStoredEntry> storedEntries = cache
            .Values.Select(LocalMediaCacheEntryMapper.ToStoredEntry)
            .ToList();

        _logger.LogInformation(
            "cache-load: executing reconciliation for {EntryCount} stored entries",
            storedEntries.Count
        );

        MediaCacheLoadExecutionResult loadExecution = _mediaCacheLoadExecutionService.Execute(
            storedEntries,
            cache.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            Probe
        );

        _logger.LogInformation(
            "cache-load: reconciliation completed with {MutationCount} mutations and {RecheckCount} recheck paths",
            loadExecution.Mutations.Count,
            loadExecution.RecheckPaths.Count
        );

        _logger.LogWarning("recheck: {count}", loadExecution.RecheckPaths.Count);
        _logger.LogInformation("cache-load: applying cache mutations");
        _mutationApplier.Apply(cache, loadExecution.Mutations);

        _logger.LogInformation(
            "cache-load: cache mutations applied, cache count {Count}",
            cache.Count
        );

        _logger.LogInformation("cache-load: updating partition sizes");
        UpdatePartitionSizes(cache);
        _logger.LogInformation("cache-load: partition sizes updated");

        if (loadExecution.RecheckPaths.Count > 0)
        {
            _logger.LogInformation("cache-load: saving primary snapshot after recheck");
            await _snapshotCoordinator.SavePrimarySnapshot([.. cache.Values], cancellationToken);
            _logger.LogInformation("cache-load: resetting incremental snapshots");
            _snapshotCoordinator.ResetIncrementalSnapshots();
        }

        _logger.LogInformation("cache-load: completed");
    }

    private MediaCacheRecheckProbeExecutionResult Probe(
        IReadOnlyList<MediaCacheRecheckProbeInput> probeInputs
    )
    {
        MediaCacheRecheckProbeExecutionResult result =
            _mediaCacheRecheckProbeExecutionService.Execute(
                probeInputs,
                probeInput =>
                {
                    bool fileExists = false;
                    long? fileSize = null;

                    if (probeInput.PartitionId is not null)
                    {
                        PartitionConfig partition = _partition.GetPath(probeInput.PartitionId);
                        string fullPath = Path.Combine(
                            [
                                _pathLayout.GetMediaPath(partition),
                                _mediaCacheEntryPathPolicyService.NormalizeForStoragePath(
                                    probeInput.Path
                                ),
                            ]
                        );
                        FileInfo fi = new(fullPath);
                        fileExists = fi.Exists;
                        fileSize = fileExists ? fi.Length : null;
                    }

                    return new MediaCacheRecheckProbeOutcome
                    {
                        Path = probeInput.Path,
                        PartitionId = probeInput.PartitionId,
                        StreamSizeBytes = probeInput.StreamSizeBytes,
                        FileExists = fileExists,
                        FileSizeBytes = fileSize,
                    };
                }
            );

        foreach (string path in result.FailedPaths)
            _logger.LogError("Error in {path}: probe execution failed", path);

        return result;
    }

    private void UpdatePartitionSizes(ConcurrentDictionary<string, MediaCacheEntry> cache)
    {
        IReadOnlyDictionary<int, long> sizes = _mediaCachePartitionSizeAggregationService.Aggregate(
            cache
                .Values.Select(LocalMediaCacheEntryMapper.ToStoredEntry)
                .Select(entry => new KeyValuePair<int?, long?>(
                    entry.PartitionId,
                    entry.FileSizeBytes
                ))
        );

        _partition.SetupSizes(sizes.ToDictionary(item => item.Key, item => item.Value));
    }
}
