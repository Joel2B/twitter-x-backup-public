using System.Collections.Concurrent;
using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Media;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Data;

internal sealed class LocalMediaCacheWriteCoordinator(
    StorageMedia config,
    IPartition partition,
    IMediaCachePartitionSelectionService mediaCachePartitionSelectionService,
    IMediaCacheWritePolicyService mediaCacheWritePolicyService,
    IMediaCacheConflictResolutionService mediaCacheConflictResolutionService,
    LocalMediaCachePathLayout pathLayout,
    LocalMediaCacheSnapshotCoordinator snapshotCoordinator,
    ILogger<LocalMediaCacheWriteCoordinator> logger
)
{
    private readonly StorageMedia _config = config;
    private readonly IPartition _partition = partition;
    private readonly IMediaCachePartitionSelectionService _mediaCachePartitionSelectionService =
        mediaCachePartitionSelectionService;
    private readonly IMediaCacheWritePolicyService _mediaCacheWritePolicyService =
        mediaCacheWritePolicyService;
    private readonly IMediaCacheConflictResolutionService _mediaCacheConflictResolutionService =
        mediaCacheConflictResolutionService;
    private readonly LocalMediaCachePathLayout _pathLayout = pathLayout;
    private readonly LocalMediaCacheSnapshotCoordinator _snapshotCoordinator = snapshotCoordinator;
    private readonly ILogger<LocalMediaCacheWriteCoordinator> _logger = logger;

    public async Task<string> GetPath(
        ConcurrentDictionary<string, MediaCacheEntry> cache,
        MediaCacheEntry? existing,
        string path,
        long size,
        CancellationToken cancellationToken = default
    )
    {
        MediaCachePartitionSelection selection = _mediaCachePartitionSelectionService.Select(
            size,
            _config.SizeHeavy,
            existing?.PartitionId
        );
        PartitionConfig partition = selection.UseHeavyPartition
            ? _partition.GetHeavy()
            : _partition.GetPath(selection.PreferredPartitionId, selection.RequestedSizeBytes);

        string fullPath = Path.Combine([_pathLayout.GetMediaPath(partition), path]);

        if (size > 0)
        {
            _logger.LogInformation(
                "media save target selected: partitionId={PartitionId}, partition={PartitionName}, path={Path}, size={Size}, existingPartitionId={ExistingPartitionId}, fullPath={FullPath}",
                partition.Id,
                partition.Name,
                path,
                size,
                existing?.PartitionId,
                fullPath
            );

            MediaCacheWritePlan writePlan = _mediaCacheWritePolicyService.BuildWritePlan(
                path,
                partition.Id,
                size
            );
            MediaCacheEntry newCache = LocalMediaCacheEntryMapper.ToCacheEntry(
                writePlan.EntryState
            );

            MediaCacheEntry stored = cache.AddOrUpdate(
                writePlan.CacheKey,
                _ => newCache,
                (_, old) =>
                {
                    MediaCacheConflictResolution conflict =
                        _mediaCacheConflictResolutionService.Resolve(old.Size?.Stream, writePlan);

                    if (conflict.Action == MediaCacheConflictAction.ThrowConflict)
                        throw new InvalidOperationException("different sizes");

                    return old;
                }
            );

            await _snapshotCoordinator.SaveIncrementalSnapshot(stored, cancellationToken);
        }

        return fullPath;
    }
}
