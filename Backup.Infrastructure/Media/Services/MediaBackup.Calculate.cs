using System.Diagnostics;
using Backup.Infrastructure.Logging;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

public partial class MediaBackup
{
    private async Task Calculate()
    {
        await ShowInfoChunks();

        MediaBackupChunkPlanningResult plan = _mediaBackupChunkPlanningService.Plan(
            _paths.Count,
            _config.Chunk.Count,
            _backup.Chunks.Path.Increase,
            _config.Chunk.Path.Increase,
            _chunks.Keys
        );

        _logger.LogInformation(
            "paths/residue: {paths}/{residue}",
            _paths.Count,
            _paths.Count % _config.Chunk.Count
        );

        _logger.LogInformation(
            "pathsPerChunk/increase/total: {paths}/{increase}/{total}",
            plan.PathsPerChunk,
            plan.IncreaseCount,
            plan.CapacityPerChunk
        );

        _logger.LogInfo("cloning chunks");

        Dictionary<int, Chunk> _chunksClone = _chunks.ToDictionary(
            o => o.Key,
            o => o.Value.Clone()
        );

        if (plan.RequiresSeedChunk)
            _chunks[0] = new() { Id = 0 };

        _logger.LogInfo("expanding chunks");

        HashSet<string> assignedCachePaths = [.. _chunks.Values.SelectMany(chunk => chunk.Data).Select(o => o.Path)];

        foreach (int missingChunkId in plan.MissingChunkIds)
            _chunks.Add(missingChunkId, new() { Id = missingChunkId });

        IReadOnlyList<MediaBackupChunkState> chunkStates = _chunks
            .Values.Select(chunk => new MediaBackupChunkState
            {
                Id = chunk.Id,
                PathCount = chunk.Data.Count,
                SizeBytes = chunk.Data.Sum(item => item.FileSize ?? 0),
            })
            .ToList();

        List<MediaBackupPathCacheObservationInput> cacheObservationInputs = [];

        foreach (string path in _paths)
        {
            MediaCacheEntry? cache = await MediaData.GetCache(path);

            cacheObservationInputs.Add(
                new MediaBackupPathCacheObservationInput
                {
                    OriginalPath = path,
                    CacheExists = cache is not null,
                    CachePath = cache?.Path,
                    FileSizeBytes = cache?.Size?.File,
                }
            );
        }

        IReadOnlyList<MediaBackupPathCacheObservation> candidateObservations =
            _mediaBackupPathObservationCompositionService.BuildPathCacheObservations(
                cacheObservationInputs
            );

        IReadOnlyList<MediaBackupPathCandidate> candidates =
            _mediaBackupPathCandidateCompositionService.Compose(
                candidateObservations,
                assignedCachePaths
            );

        MediaBackupChunkAssignmentResult assignment = _mediaBackupChunkAssignmentService.Assign(
            chunkStates,
            candidates,
            _backup.Chunks.Total,
            plan.PathsPerChunk,
            plan.IncreaseCount,
            _config.Chunk.Path.Size
        );

        _logger.LogInfo("current chunk: {chunk}", assignment.InitialChunkId);

        List<string> newPaths = [];

        foreach (MediaBackupPathAssignment item in assignment.Assignments)
        {
            _chunks[item.ChunkId].Data.Add(new() { Path = item.CachePath });
            newPaths.Add(item.OriginalPath);
        }

        IReadOnlyList<MediaBackupChunkPathsState> beforeChunkPaths = _chunksClone
            .Values.Select(chunk => new MediaBackupChunkPathsState
            {
                Id = chunk.Id,
                Paths = chunk.Data.Select(data => data.Path).ToList(),
            })
            .ToList();

        IReadOnlyList<MediaBackupChunkPathsState> afterChunkPaths = _chunks
            .Values.Select(chunk => new MediaBackupChunkPathsState
            {
                Id = chunk.Id,
                Paths = chunk.Data.Select(data => data.Path).ToList(),
            })
            .ToList();

        IReadOnlyList<MediaBackupChunkCountState> beforeCountStates =
            _mediaBackupChunkSnapshotCompositionService.BuildChunkCountStates(beforeChunkPaths);
        IReadOnlyList<MediaBackupChunkCountState> afterCountStates =
            _mediaBackupChunkSnapshotCompositionService.BuildChunkCountStates(afterChunkPaths);

        MediaBackupChunkCountDeltaResult deltas = _mediaBackupChunkCountDeltaService.Compare(
            beforeCountStates,
            afterCountStates
        );

        MediaBackupChunkPathMaps pathMaps =
            _mediaBackupChunkSnapshotCompositionService.BuildPathMaps(
                beforeChunkPaths,
                afterChunkPaths
            );

        Dictionary<string, long> sizeByPath = [];

        foreach (string path in pathMaps.DistinctPathsForSizeLookup)
        {
            MediaCacheEntry? cache = await MediaData.GetCache(path);

            if (cache is null)
                continue;

            sizeByPath[path] = cache.Size?.File ?? 0;
        }

        IReadOnlyList<MediaBackupChunkDeltaLogInput> deltaLogInputs =
            _mediaBackupChunkDeltaInputCompositionService.Compose(
                deltas.Items,
                pathMaps.BeforePathsByChunk,
                pathMaps.AfterPathsByChunk,
                sizeByPath
            );

        MediaBackupChunkDeltaLogPlan deltaLogPlan = _mediaBackupChunkDeltaLogPlanningService.Plan(
            deltaLogInputs,
            deltas.TotalAddedPaths,
            newPaths.Count
        );

        if (deltaLogPlan.Rows.Count > 0)
        {
            _logger.LogInfo(
                "{id,-3} {before,-6} {after,-6} {diff,-6} {sizeBefore} {sizeAfter}",
                "id",
                "before",
                "after",
                "diff",
                "size before (GiB)",
                "size after (GiB)"
            );

            foreach (MediaBackupChunkDeltaLogRow row in deltaLogPlan.Rows)
            {
                _logger.LogInformation(
                    "{id,-3} {before,-6} {after,-6} {diff,-6} {sizeBefore,-17} {sizeAfter}",
                    row.ChunkId,
                    row.BeforeCount,
                    row.AfterCount,
                    row.Difference,
                    row.SizeBeforeGiB,
                    row.SizeAfterGiB
                );
            }
        }

        _logger.LogInformation(
            "{paths1}/{paths2} new paths",
            deltaLogPlan.TotalAddedPaths,
            deltaLogPlan.AddedPathCount
        );
    }

    private async Task CalculateDirect()
    {
        CancellationTokenSource cts = new();

        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = 64,
            CancellationToken = cts.Token,
        };

        int total = _paths.Count;
        int done = 0;
        int lastPercent = -1;
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            await Parallel.ForEachAsync(
                _paths,
                options,
                async (path, ct) =>
                {
                    try
                    {
                        MediaCacheEntry? cache = await MediaData.GetCache(path);
                        bool existsSource = await MediaData.Exists(path);
                        bool existsTarget = await _mediaBackupData.Exists(path);

                        MediaBackupDirectPathScanResult result =
                            _mediaBackupDirectPathScanOrchestrationService.Evaluate(
                                _mediaBackupPathObservationCompositionService.BuildDirectPathObservation(
                                    new MediaBackupDirectPathObservationInput
                                    {
                                        Path = path,
                                        CacheExists = cache is not null,
                                        CachePath = cache?.Path,
                                        FileSizeBytes = cache?.Size?.File,
                                        SourceExists = existsSource,
                                        TargetExists = existsTarget,
                                        MaxPathSizeBytes = _config.Chunk.Path.Size,
                                    }
                                )
                            );

                        if (result.ShouldThrowMissingSource)
                            throw new Exception();

                        if (!result.ShouldIncludeDirectPath)
                            return;

                        _pathsDirect.Add(result.IncludedPath);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Canceled {path}", path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "error in {path}: {error}", path, ex.Message);
                    }
                    finally
                    {
                        int current = Interlocked.Increment(ref done);
                        int prev = Volatile.Read(ref lastPercent);
                        MediaBackupProgressDecision progress =
                            _mediaBackupProgressPolicyService.Evaluate(current, total, prev);

                        if (progress.ShouldLog)
                        {
                            if (
                                Interlocked.CompareExchange(
                                    ref lastPercent,
                                    progress.Percent,
                                    prev
                                ) == prev
                            )
                            {
                                _logger.LogInformation(
                                    "Progress: {percent}% ({current}/{total}) elapsed={elapsed}",
                                    progress.Percent,
                                    current,
                                    total,
                                    sw.Elapsed
                                );
                            }
                        }
                    }
                }
            );
        }
        catch (OperationCanceledException) { }

        List<string> pathsInChunks = _chunks
            .Values.SelectMany(o => o.Data)
            .Select(o => o.Path)
            .ToList();

        MediaBackupDirectPathFinalizeResult finalize = _mediaBackupDirectPathFinalizeService.Finalize(
            pathsInChunks,
            _pathsDirect
        );

        _pathsInBoth = finalize.PathsInBoth.ToList();

        _logger.LogInformation("{paths} in both", _pathsInBoth.Count);

        _pathsDirect = [.. finalize.DirectPaths];

        _logger.LogInformation(
            "{paths} paths > {size}",
            _pathsDirect.Count,
            _config.Chunk.Path.Size
        );
    }
}
