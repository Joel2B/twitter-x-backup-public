using System.Collections.Concurrent;
using Backup.Application.IO;
using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Media.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Media.Data;

public class LocalMediaCache(
    ILogger<LocalMediaCache> _logger,
    StorageMedia _config,
    IPartition _partition,
    IDataStoreGuardService dataStoreGuardService,
    IMediaCacheDirectoryPolicyService mediaCacheDirectoryPolicyService,
    IMediaCacheRecheckPlanningService mediaCacheRecheckPlanningService,
    IMediaCacheRecheckObservationCompositionService mediaCacheRecheckObservationCompositionService,
    IMediaCacheRecheckEvaluationService mediaCacheRecheckEvaluationService,
    IMediaCacheRecheckMutationPlanningService mediaCacheRecheckMutationPlanningService,
    IMediaCacheJsonSnapshotService mediaCacheJsonSnapshotService,
    IMediaCacheEntryPathPolicyService mediaCacheEntryPathPolicyService,
    IMediaCacheEntryStateFactoryService mediaCacheEntryStateFactoryService,
    IMediaCacheWritePolicyService mediaCacheWritePolicyService,
    IMediaCachePartitionSelectionService mediaCachePartitionSelectionService,
    IMediaCacheStoredEntryProjectionService mediaCacheStoredEntryProjectionService,
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
    private readonly IMediaCacheRecheckPlanningService _mediaCacheRecheckPlanningService =
        mediaCacheRecheckPlanningService;
    private readonly IMediaCacheRecheckObservationCompositionService _mediaCacheRecheckObservationCompositionService =
        mediaCacheRecheckObservationCompositionService;
    private readonly IMediaCacheRecheckEvaluationService _mediaCacheRecheckEvaluationService =
        mediaCacheRecheckEvaluationService;
    private readonly IMediaCacheRecheckMutationPlanningService _mediaCacheRecheckMutationPlanningService =
        mediaCacheRecheckMutationPlanningService;
    private readonly IMediaCacheJsonSnapshotService _mediaCacheJsonSnapshotService =
        mediaCacheJsonSnapshotService;
    private readonly IMediaCacheEntryPathPolicyService _mediaCacheEntryPathPolicyService =
        mediaCacheEntryPathPolicyService;
    private readonly IMediaCacheEntryStateFactoryService _mediaCacheEntryStateFactoryService =
        mediaCacheEntryStateFactoryService;
    private readonly IMediaCacheWritePolicyService _mediaCacheWritePolicyService =
        mediaCacheWritePolicyService;
    private readonly IMediaCachePartitionSelectionService _mediaCachePartitionSelectionService =
        mediaCachePartitionSelectionService;
    private readonly IMediaCacheStoredEntryProjectionService _mediaCacheStoredEntryProjectionService =
        mediaCacheStoredEntryProjectionService;
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

                await foreach (
                    MediaCacheEntry entry in LocalMediaCacheReader.Get(
                        file,
                        _mediaCacheJsonSnapshotService
                    )
                )
                    _cache[entry.Path] = entry;
            }

            _logger.LogWarning("cache: {count}", _cache.Count);
            recheck = _mediaCacheRecheckPlanningService.SelectPathsToRecheck(
                _cache.Values.Select(ToStoredEntry).ToList()
            );
        }
        else
            _logger.LogWarning("cache file not exist, {path}", file);

        _logger.LogWarning("recheck: {count}", recheck.Count);
        IReadOnlyList<MediaCacheRecheckProbeInput> probeInputs =
            _mediaCacheRecheckObservationCompositionService.BuildProbeInputs(
                recheck,
                _cache.Values.Select(ToStoredEntry).ToList()
            );
        IReadOnlyList<MediaCacheRecheckObservation> observations = BuildRecheckObservations(probeInputs);
        IReadOnlyList<MediaCacheRecheckEvaluation> evaluations = _mediaCacheRecheckEvaluationService.Evaluate(
            observations
        );
        IReadOnlyList<MediaCacheRecheckMutation> mutations =
            _mediaCacheRecheckMutationPlanningService.Plan(
                evaluations,
                _cache.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            );
        ApplyRecheckMutations(mutations);

        IReadOnlyDictionary<int, long> sizes = _mediaCachePartitionSizeAggregationService.Aggregate(
            _mediaCacheStoredEntryProjectionService.ToPartitionFileSizes(
                _cache.Values.Select(ToStoredEntry)
            )
        );

        _partition.SetupSizes(sizes.ToDictionary(item => item.Key, item => item.Value));

        if (recheck.Count > 0)
        {
            await LocalMediaCacheReader.Save(file, [.. _cache.Values], _mediaCacheJsonSnapshotService);
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

    private Task Replicate()
    {
        string primaryFilePath = GetPathCacheFilePrimary();

        if (!File.Exists(primaryFilePath))
            return Task.CompletedTask;

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

        return Task.CompletedTask;
    }

    private async Task SaveCache(MediaCacheEntry cache, CancellationToken ct)
    {
        PartitionConfig primary = _partition.GetPrimary();
        string pathCache = GetPathCacheDownload(primary);
        string fileName = _mediaCacheEntryPathPolicyService.BuildCacheSnapshotFileName(
            cache.Path,
            cache.PartitionId
        );
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
                    if (
                        _mediaCacheWritePolicyService.HasConflict(old.Size?.Stream, writePlan)
                    )
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

    private IReadOnlyList<MediaCacheRecheckObservation> BuildRecheckObservations(
        IReadOnlyList<MediaCacheRecheckProbeInput> probeInputs
    )
    {
        List<MediaCacheRecheckProbeOutcome> outcomes = [];

        foreach (MediaCacheRecheckProbeInput probeInput in probeInputs)
        {
            try
            {
                bool fileExists = false;
                long? fileSize = null;

                if (probeInput.PartitionId is not null)
                {
                    PartitionConfig partition = _partition.GetPath(probeInput.PartitionId);
                    string fullPath = Path.Combine(
                        [
                            GetPathMedia(partition),
                            _mediaCacheEntryPathPolicyService.NormalizeForStoragePath(probeInput.Path),
                        ]
                    );
                    FileInfo fi = new(fullPath);
                    fileExists = fi.Exists;
                    fileSize = fileExists ? fi.Length : null;
                }

                outcomes.Add(
                    new MediaCacheRecheckProbeOutcome
                    {
                        Path = probeInput.Path,
                        PartitionId = probeInput.PartitionId,
                        StreamSizeBytes = probeInput.StreamSizeBytes,
                        FileExists = fileExists,
                        FileSizeBytes = fileSize,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in {path}: {error}", probeInput.Path, ex.Message);
            }
        }

        return _mediaCacheRecheckObservationCompositionService.ToObservations(outcomes);
    }

    private void ApplyRecheckMutations(IReadOnlyList<MediaCacheRecheckMutation> mutations)
    {
        foreach (MediaCacheRecheckMutation mutation in mutations)
        {
            switch (mutation.Kind)
            {
                case MediaCacheRecheckMutationKind.Invalid:
                    _logger.LogError("invalid recheck evaluation for path {path}", mutation.Path);
                    break;
                case MediaCacheRecheckMutationKind.Remove:
                {
                    bool removed = _cache.TryRemove(mutation.Path, out _);

                    if (removed)
                        _logger.LogWarning("{path} path removed from cache", mutation.Path);
                    else
                        _logger.LogError("error removing path {path}", mutation.Path);
                    break;
                }
                case MediaCacheRecheckMutationKind.Update:
                {
                    if (mutation.UpdatedEntryState is null)
                        break;

                    if (!_cache.TryGetValue(mutation.Path, out MediaCacheEntry? old))
                        break;

                    MediaCacheEntry updated = ToCacheEntry(mutation.UpdatedEntryState);
                    _cache.TryUpdate(mutation.Path, updated, old);
                    break;
                }
                case MediaCacheRecheckMutationKind.None:
                case MediaCacheRecheckMutationKind.SkipMissing:
                default:
                    break;
            }
        }
    }

    private static MediaCacheEntry ToCacheEntry(MediaCacheEntryState state)
        => new()
        {
            Path = state.Path,
            PartitionId = state.PartitionId,
            Size = new MediaCacheSize
            {
                Stream = state.StreamSizeBytes,
                File = state.FileSizeBytes,
            },
        };

    private static MediaCacheStoredEntry ToStoredEntry(MediaCacheEntry entry)
        => new()
        {
            Path = entry.Path,
            PartitionId = entry.PartitionId,
            StreamSizeBytes = entry.Size?.Stream,
            FileSizeBytes = entry.Size?.File,
        };
}
