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
    IMediaCacheStoredEntryProjectionService mediaCacheStoredEntryProjectionService,
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
    private readonly IMediaCacheStoredEntryProjectionService _mediaCacheStoredEntryProjectionService =
        mediaCacheStoredEntryProjectionService;
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
        await _snapshotCoordinator.ReplicatePrimarySnapshot(cancellationToken);

        string file = _snapshotCoordinator.GetPrimaryFilePath();

        if (File.Exists(file))
        {
            if (cache.IsEmpty)
            {
                await _snapshotCoordinator.LoadIncrementalSnapshotsInto(cache, cancellationToken);
                await _snapshotCoordinator.LoadPrimarySnapshotInto(cache, cancellationToken);
            }

            _logger.LogWarning("cache: {count}", cache.Count);
        }
        else
            _logger.LogWarning("cache file not exist, {path}", file);

        IReadOnlyList<MediaCacheStoredEntry> storedEntries = cache
            .Values.Select(LocalMediaCacheEntryMapper.ToStoredEntry)
            .ToList();
        MediaCacheLoadExecutionResult loadExecution = _mediaCacheLoadExecutionService.Execute(
            storedEntries,
            cache.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            Probe
        );

        _logger.LogWarning("recheck: {count}", loadExecution.RecheckPaths.Count);
        _mutationApplier.Apply(cache, loadExecution.Mutations);
        UpdatePartitionSizes(cache);

        if (loadExecution.RecheckPaths.Count > 0)
        {
            await _snapshotCoordinator.SavePrimarySnapshot([.. cache.Values], cancellationToken);
            _snapshotCoordinator.ResetIncrementalSnapshots();
        }
    }

    private MediaCacheRecheckProbeExecutionResult Probe(IReadOnlyList<MediaCacheRecheckProbeInput> probeInputs)
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
            _mediaCacheStoredEntryProjectionService.ToPartitionFileSizes(
                cache.Values.Select(LocalMediaCacheEntryMapper.ToStoredEntry)
            )
        );

        _partition.SetupSizes(sizes.ToDictionary(item => item.Key, item => item.Value));
    }
}
