using System.Collections.Concurrent;
using Backup.Application.IO;
using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Media;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Data;

// Facade for media cache IO and reconciliation flows. It coordinates cache
// collaborators but preserves the existing cache file and path behavior.
public class LocalMediaCache(
    ILogger<LocalMediaCache> _logger,
    StorageMedia _config,
    IPartition _partition,
    LocalMediaCacheDependencies dependencies
) : IMediaCache
{
    private readonly ILogger<LocalMediaCache> _logger = _logger;
    private readonly StorageMedia _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IDataStoreGuardService _dataStoreGuardService =
        dependencies.DataStoreGuardService;
    private readonly IMediaCacheDirectoryPolicyService _mediaCacheDirectoryPolicyService =
        dependencies.MediaCacheDirectoryPolicyService;
    private readonly IMediaCacheLoadExecutionService _mediaCacheLoadExecutionService =
        dependencies.MediaCacheLoadExecutionService;
    private readonly IMediaCacheRecheckProbeExecutionService _mediaCacheRecheckProbeExecutionService =
        dependencies.MediaCacheRecheckProbeExecutionService;
    private readonly IMediaCacheRecheckMutationExecutionService _mediaCacheRecheckMutationExecutionService =
        dependencies.MediaCacheRecheckMutationExecutionService;
    private readonly IMediaCachePersistenceIOService _mediaCachePersistenceIOService =
        dependencies.MediaCachePersistenceIOService;
    private readonly IMediaCacheEntryPathPolicyService _mediaCacheEntryPathPolicyService =
        dependencies.MediaCacheEntryPathPolicyService;
    private readonly IMediaCacheEntryStateFactoryService _mediaCacheEntryStateFactoryService =
        dependencies.MediaCacheEntryStateFactoryService;
    private readonly IMediaCacheWritePolicyService _mediaCacheWritePolicyService =
        dependencies.MediaCacheWritePolicyService;
    private readonly IMediaCacheConflictResolutionService _mediaCacheConflictResolutionService =
        dependencies.MediaCacheConflictResolutionService;
    private readonly IMediaCachePartitionSelectionService _mediaCachePartitionSelectionService =
        dependencies.MediaCachePartitionSelectionService;
    private readonly IMediaCacheStoredEntryProjectionService _mediaCacheStoredEntryProjectionService =
        dependencies.MediaCacheStoredEntryProjectionService;
    private readonly IMediaCachePartitionSizeAggregationService _mediaCachePartitionSizeAggregationService =
        dependencies.MediaCachePartitionSizeAggregationService;
    private readonly IMediaCacheReplicationPathService _mediaCacheReplicationPathService =
        dependencies.MediaCacheReplicationPathService;

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
        string fileName = _dataStoreGuardService.RequireConfiguredFileName(
            _config.Paths.Cache.File
        );

        return Path.Combine(GetPathCache(partition), fileName);
    }

    public string GetPathCacheFilePrimary() => GetPathCacheFile(_partition.GetPrimary());

    public async Task Load()
    {
        await Replicate();

        string file = GetPathCacheFilePrimary();

        if (File.Exists(file))
        {
            if (_cache.IsEmpty)
            {
                await LoadCache();

                IReadOnlyList<MediaCacheEntry> entries =
                    await _mediaCachePersistenceIOService.LoadPrimarySnapshot(file);
                foreach (MediaCacheEntry entry in entries)
                    _cache[entry.Path] = entry;
            }

            _logger.LogWarning("cache: {count}", _cache.Count);
        }
        else
            _logger.LogWarning("cache file not exist, {path}", file);

        IReadOnlyList<MediaCacheStoredEntry> storedEntries = _cache
            .Values.Select(ToStoredEntry)
            .ToList();
        MediaCacheLoadExecutionResult loadExecution = _mediaCacheLoadExecutionService.Execute(
            storedEntries,
            _cache.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            probeInputs =>
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
                                PartitionConfig partition = _partition.GetPath(
                                    probeInput.PartitionId
                                );
                                string fullPath = Path.Combine(
                                    [
                                        GetPathMedia(partition),
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
        );

        _logger.LogWarning("recheck: {count}", loadExecution.RecheckPaths.Count);
        ApplyRecheckMutations(loadExecution.Mutations);

        IReadOnlyDictionary<int, long> sizes = _mediaCachePartitionSizeAggregationService.Aggregate(
            _mediaCacheStoredEntryProjectionService.ToPartitionFileSizes(
                _cache.Values.Select(ToStoredEntry)
            )
        );

        _partition.SetupSizes(sizes.ToDictionary(item => item.Key, item => item.Value));

        if (loadExecution.RecheckPaths.Count > 0)
        {
            await _mediaCachePersistenceIOService.SavePrimarySnapshot(file, [.. _cache.Values]);
            DeleteCache();
        }
    }

    private async Task LoadCache()
    {
        PartitionConfig primary = _partition.GetPrimary();
        string directory = GetPathCacheDownload(primary);
        IReadOnlyList<MediaCacheEntry> snapshots =
            await _mediaCachePersistenceIOService.LoadIncrementalSnapshots(directory);

        foreach (MediaCacheEntry cache in snapshots)
            _cache.TryAdd(cache.Path, cache);
    }

    private Task Replicate()
    {
        string primaryFilePath = GetPathCacheFilePrimary();

        List<PartitionConfig> partitions = _partition.GetCache();
        IReadOnlyList<string> replicaPaths = _mediaCacheReplicationPathService.GetReplicaPaths(
            primaryFilePath,
            partitions.Select(GetPathCacheFile)
        );

        return _mediaCachePersistenceIOService.ReplicatePrimarySnapshot(
            primaryFilePath,
            replicaPaths
        );
    }

    private async Task SaveCache(MediaCacheEntry cache, CancellationToken ct)
    {
        PartitionConfig primary = _partition.GetPrimary();
        string directory = GetPathCacheDownload(primary);
        string fileName = _mediaCacheEntryPathPolicyService.BuildCacheSnapshotFileName(
            cache.Path,
            cache.PartitionId
        );
        await _mediaCachePersistenceIOService.SaveIncrementalSnapshot(
            directory,
            cache,
            fileName,
            ct
        );
    }

    private void DeleteCache()
    {
        PartitionConfig primary = _partition.GetPrimary();
        string directory = GetPathCacheDownload(primary);
        _mediaCachePersistenceIOService.ResetIncrementalSnapshotDirectory(directory);
    }

    public async Task<string> GetPath(string path, long size = 0, CancellationToken ct = default)
    {
        MediaCacheEntry? cache = Get(path);
        MediaCachePartitionSelection selection = _mediaCachePartitionSelectionService.Select(
            size,
            _config.SizeHeavy,
            cache?.PartitionId
        );
        PartitionConfig partition = selection.UseHeavyPartition
            ? _partition.GetHeavy()
            : _partition.GetPath(selection.PreferredPartitionId, selection.RequestedSizeBytes);

        if (size > 0)
        {
            MediaCacheWritePlan writePlan = _mediaCacheWritePolicyService.BuildWritePlan(
                path,
                partition.Id,
                size
            );
            MediaCacheEntry newCache = ToCacheEntry(writePlan.EntryState);

            _cache.AddOrUpdate(
                writePlan.CacheKey,
                _ => newCache,
                (_, old) =>
                {
                    MediaCacheConflictResolution conflict =
                        _mediaCacheConflictResolutionService.Resolve(old.Size?.Stream, writePlan);

                    if (conflict.Action == MediaCacheConflictAction.ThrowConflict)
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
        path = _mediaCacheEntryPathPolicyService.NormalizeForCacheKey(path);
        _cache.TryGetValue(path, out MediaCacheEntry? cache);

        return cache;
    }

    private void ApplyRecheckMutations(IReadOnlyList<MediaCacheRecheckMutation> mutations)
    {
        MediaCacheRecheckMutationApplySelection selection =
            _mediaCacheRecheckMutationExecutionService.Execute(
                mutations,
                _cache.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            );

        foreach (string path in selection.InvalidPaths)
        {
            _logger.LogError("invalid recheck evaluation for path {path}", path);
        }

        foreach (string path in selection.RemoveExistingPaths)
        {
            _cache.TryRemove(path, out _);
            _logger.LogWarning("{path} path removed from cache", path);
        }

        foreach (string path in selection.RemoveMissingPaths)
        {
            _logger.LogError("error removing path {path}", path);
        }

        foreach (MediaCacheEntryState state in selection.UpdateExistingEntries)
        {
            _cache.TryGetValue(state.Path, out MediaCacheEntry? old);
            MediaCacheEntry updated = ToCacheEntry(state);
            if (old is not null)
                _cache.TryUpdate(state.Path, updated, old);
        }

        foreach (string path in selection.UpdateMissingPaths)
        {
            _logger.LogError("error updating path {path}", path);
        }
    }

    private static MediaCacheEntry ToCacheEntry(MediaCacheEntryState state) =>
        new()
        {
            Path = state.Path,
            PartitionId = state.PartitionId,
            Size = new MediaCacheSize
            {
                Stream = state.StreamSizeBytes,
                File = state.FileSizeBytes,
            },
        };

    private static MediaCacheStoredEntry ToStoredEntry(MediaCacheEntry entry) =>
        new()
        {
            Path = entry.Path,
            PartitionId = entry.PartitionId,
            StreamSizeBytes = entry.Size?.Stream,
            FileSizeBytes = entry.Size?.File,
        };
}
