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

        Dictionary<string, ChunkData> data = _chunks
            .Values.SelectMany(chunk => chunk.Data)
            .ToDictionary(o => o.Path, o => o);

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

        List<MediaBackupPathCandidate> candidates = [];

        foreach (string path in _paths)
        {
            MediaCacheEntry? cache = await MediaData.GetCache(path);

            if (cache is null)
                continue;

            candidates.Add(
                new MediaBackupPathCandidate
                {
                    OriginalPath = path,
                    CachePath = cache.Path,
                    FileSizeBytes = cache.Size?.File,
                    IsAlreadyAssigned = data.ContainsKey(cache.Path),
                }
            );
        }

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

        MediaBackupChunkCountDeltaResult deltas = _mediaBackupChunkCountDeltaService.Compare(
            _chunksClone.Values.Select(chunk => new MediaBackupChunkCountState
            {
                ChunkId = chunk.Id,
                PathCount = chunk.Data.Count,
            }),
            _chunks.Values.Select(chunk => new MediaBackupChunkCountState
            {
                ChunkId = chunk.Id,
                PathCount = chunk.Data.Count,
            })
        );

        List<MediaBackupChunkDeltaLogInput> deltaLogInputs = [];

        foreach (MediaBackupChunkCountDeltaItem delta in deltas.Items)
        {
            _chunksClone.TryGetValue(delta.ChunkId, out Chunk? chunkBefore);

            long sizeBefore = 0;
            long sizeAfter = 0;

            foreach (ChunkData chunkData in chunkBefore?.Data ?? [])
            {
                MediaCacheEntry? cache = await MediaData.GetCache(chunkData.Path);

                if (cache is not null)
                    sizeBefore += cache.Size?.File ?? 0;
            }

            foreach (ChunkData chunkData in _chunks[delta.ChunkId].Data)
            {
                MediaCacheEntry? cache = await MediaData.GetCache(chunkData.Path);

                if (cache is not null)
                    sizeAfter += cache.Size?.File ?? 0;
            }

            deltaLogInputs.Add(
                new MediaBackupChunkDeltaLogInput
                {
                    ChunkId = delta.ChunkId,
                    BeforeCount = delta.BeforeCount,
                    AfterCount = delta.AfterCount,
                    Difference = delta.Difference,
                    SizeBeforeBytes = sizeBefore,
                    SizeAfterBytes = sizeAfter,
                }
            );
        }

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

                        MediaBackupDirectPathCandidateDecision decision =
                            _mediaBackupDirectPathCandidateDecisionService.Decide(
                                new MediaBackupDirectPathCandidateObservation
                                {
                                    Path = path,
                                    CacheExists = cache is not null,
                                    CachePath = cache?.Path ?? string.Empty,
                                    FileSizeBytes = cache?.Size?.File,
                                    SourceExists = existsSource,
                                    TargetExists = existsTarget,
                                    MaxPathSizeBytes = _config.Chunk.Path.Size,
                                }
                            );

                        if (decision.ShouldThrowMissingSource)
                            throw new Exception();

                        if (!decision.ShouldIncludeDirectPath)
                            return;

                        _pathsDirect.Add(cache!.Path);
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
                        int percent = (int)((long)current * 100 / total);
                        int prev = Volatile.Read(ref lastPercent);

                        if (percent != prev)
                        {
                            if (
                                Interlocked.CompareExchange(ref lastPercent, percent, prev) == prev
                            )
                            {
                                _logger.LogInformation(
                                    "Progress: {percent}% ({current}/{total}) elapsed={elapsed}",
                                    percent,
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

        IReadOnlyList<string> normalizedDirectPaths = _mediaBackupDirectPathQueueService.Normalize(
            _pathsDirect
        );

        MediaBackupDirectPathSelectionResult selection = _mediaBackupDirectPathSelectionService.Select(
            pathsInChunks,
            normalizedDirectPaths
        );

        _pathsInBoth = selection.PathsInBoth.ToList();

        _logger.LogInformation("{paths} in both", _pathsInBoth.Count);

        _pathsDirect = [.. _mediaBackupDirectPathQueueService.Normalize(selection.DirectPaths)];

        _logger.LogInformation(
            "{paths} paths > {size}",
            _pathsDirect.Count,
            _config.Chunk.Path.Size
        );
    }
}
