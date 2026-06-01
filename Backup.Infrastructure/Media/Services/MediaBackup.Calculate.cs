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

        _logger.LogInfo("cloning chunks");

        Dictionary<int, Chunk> _chunksClone = _chunks.ToDictionary(
            o => o.Key,
            o => o.Value.Clone()
        );

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

        IReadOnlyList<MediaBackupChunkPathsState> beforeChunkPaths =
            _mediaBackupChunkRuntimeCompositionService.BuildChunkPathStates(
                _chunksClone.Values.Select(chunk => new MediaBackupChunkPathsInput
                {
                    Id = chunk.Id,
                    Paths = chunk.Data.Select(data => data.Path).ToList(),
                })
            );
        IReadOnlyList<MediaBackupChunkStateInput> chunkStateInputs = _chunks
            .Values.Select(chunk => new MediaBackupChunkStateInput
            {
                Id = chunk.Id,
                PathCount = chunk.Data.Count,
                SizeBytes = chunk.Data.Sum(item => item.FileSize ?? 0),
            })
            .ToList();
        HashSet<string> assignedCachePaths = [.. _chunks.Values.SelectMany(chunk => chunk.Data).Select(o => o.Path)];

        IReadOnlyDictionary<string, long> sizeByPath = cacheObservationInputs
            .Where(input => !string.IsNullOrWhiteSpace(input.CachePath))
            .GroupBy(input => input.CachePath!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.FirstOrDefault(entry => entry.FileSizeBytes.HasValue)?.FileSizeBytes
                    ?? 0,
                StringComparer.Ordinal
            );

        MediaBackupCalculateExecutionResult calculation = _mediaBackupCalculateExecutionService.Execute(
            new MediaBackupCalculateExecutionInput
            {
                TotalPathCount = _paths.Count,
                ChunkCount = _backup.Chunks.Total,
                BackupIncreaseCount = _backup.Chunks.Path.Increase,
                ConfigIncreaseCount = _config.Chunk.Path.Increase,
                ExistingChunkIds = _chunks.Keys.ToList(),
                ChunkStateInputs = chunkStateInputs,
                AssignedCachePaths = assignedCachePaths.ToList(),
                CacheObservationInputs = cacheObservationInputs,
                BeforeChunkPaths = beforeChunkPaths,
                SizeByPath = sizeByPath,
                MaxPathSizeBytes = _config.Chunk.Path.Size,
            }
        );

        MediaBackupChunkPlanningResult plan = calculation.Planning;

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

        if (plan.RequiresSeedChunk)
            _chunks[0] = new() { Id = 0 };

        _logger.LogInfo("expanding chunks");

        foreach (int missingChunkId in plan.MissingChunkIds)
        {
            if (!_chunks.ContainsKey(missingChunkId))
                _chunks.Add(missingChunkId, new() { Id = missingChunkId });
        }

        _logger.LogInfo("current chunk: {chunk}", calculation.Assignment.InitialChunkId);

        foreach (
            (
                int chunkId,
                IReadOnlyList<string> addedPaths
            ) in calculation.ApplyAssignments.AddedCachePathsByChunk
        )
        {
            if (!_chunks.TryGetValue(chunkId, out Chunk? chunk))
                continue;

            foreach (string path in addedPaths)
                chunk.Data.Add(new() { Path = path });
        }

        MediaBackupChunkDeltaLogPlan deltaLogPlan = calculation.DeltaLogPlan;

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
                calculation.ApplyAssignments.AddedOriginalPaths.Count
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
