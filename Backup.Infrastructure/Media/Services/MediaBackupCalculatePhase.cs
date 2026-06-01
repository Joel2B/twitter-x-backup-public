using System.Diagnostics;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupCalculatePhase : IMediaBackupCalculatePhase
{
    public async Task Calculate(MediaBackupRuntime runtime, string? backupId)
    {
        await runtime.ShowInfoChunks(backupId);

        runtime.Logger.LogInfo("cloning chunks");

        Dictionary<int, Chunk> chunksClone = runtime.Context.Chunks.ToDictionary(
            o => o.Key,
            o => o.Value.Clone()
        );

        List<MediaBackupPathCacheObservationInput> cacheObservationInputs = [];

        foreach (string path in runtime.Context.Paths)
        {
            MediaCacheEntry? cache = await runtime.MediaData.GetCache(path);

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
            runtime.Dependencies.ChunkRuntimeCompositionService.BuildChunkPathStates(
                chunksClone.Values.Select(chunk => new MediaBackupChunkPathsInput
                {
                    Id = chunk.Id,
                    Paths = chunk.Data.Select(data => data.Path).ToList(),
                })
            );
        IReadOnlyList<MediaBackupChunkStateInput> chunkStateInputs = runtime
            .Context.Chunks.Values.Select(chunk => new MediaBackupChunkStateInput
            {
                Id = chunk.Id,
                PathCount = chunk.Data.Count,
                SizeBytes = chunk.Data.Sum(item => item.FileSize ?? 0),
            })
            .ToList();
        HashSet<string> assignedCachePaths =
        [
            .. runtime.Context.Chunks.Values.SelectMany(chunk => chunk.Data).Select(o => o.Path),
        ];

        IReadOnlyDictionary<string, long> sizeByPath = cacheObservationInputs
            .Where(input => !string.IsNullOrWhiteSpace(input.CachePath))
            .GroupBy(input => input.CachePath!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                    group.FirstOrDefault(entry => entry.FileSizeBytes.HasValue)?.FileSizeBytes ?? 0,
                StringComparer.Ordinal
            );

        MediaBackupCalculateExecutionResult calculation =
            runtime.Dependencies.CalculateExecutionService.Execute(
                new MediaBackupCalculateExecutionInput
                {
                    TotalPathCount = runtime.Context.Paths.Count,
                    ChunkCount = runtime.Context.Backup.Chunks.Total,
                    BackupIncreaseCount = runtime.Context.Backup.Chunks.Path.Increase,
                    ConfigIncreaseCount = runtime.Config.Chunk.Path.Increase,
                    ExistingChunkIds = runtime.Context.Chunks.Keys.ToList(),
                    ChunkStateInputs = chunkStateInputs,
                    AssignedCachePaths = assignedCachePaths.ToList(),
                    CacheObservationInputs = cacheObservationInputs,
                    BeforeChunkPaths = beforeChunkPaths,
                    SizeByPath = sizeByPath,
                    MaxPathSizeBytes = runtime.Config.Chunk.Path.Size,
                }
            );

        MediaBackupChunkPlanningResult plan = calculation.Planning;

        runtime.Logger.LogInformation(
            "paths/residue: {paths}/{residue}",
            runtime.Context.Paths.Count,
            runtime.Context.Paths.Count % runtime.Config.Chunk.Count
        );

        runtime.Logger.LogInformation(
            "pathsPerChunk/increase/total: {paths}/{increase}/{total}",
            plan.PathsPerChunk,
            plan.IncreaseCount,
            plan.CapacityPerChunk
        );

        if (plan.RequiresSeedChunk)
            runtime.Context.Chunks[0] = new() { Id = 0 };

        runtime.Logger.LogInfo("expanding chunks");

        foreach (int missingChunkId in plan.MissingChunkIds)
        {
            if (!runtime.Context.Chunks.ContainsKey(missingChunkId))
                runtime.Context.Chunks.Add(missingChunkId, new() { Id = missingChunkId });
        }

        runtime.Logger.LogInfo("current chunk: {chunk}", calculation.Assignment.InitialChunkId);

        foreach (
            (int chunkId, IReadOnlyList<string> addedPaths) in calculation
                .ApplyAssignments
                .AddedCachePathsByChunk
        )
        {
            if (!runtime.Context.Chunks.TryGetValue(chunkId, out Chunk? chunk))
                continue;

            foreach (string path in addedPaths)
                chunk.Data.Add(new() { Path = path });
        }

        MediaBackupChunkDeltaLogPlan deltaLogPlan = calculation.DeltaLogPlan;

        if (deltaLogPlan.Rows.Count > 0)
        {
            runtime.Logger.LogInfo(
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
                runtime.Logger.LogInformation(
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

        runtime.Logger.LogInformation(
            "{paths1}/{paths2} new paths",
            deltaLogPlan.TotalAddedPaths,
            calculation.ApplyAssignments.AddedOriginalPaths.Count
        );
    }

    public async Task CalculateDirect(MediaBackupRuntime runtime)
    {
        CancellationTokenSource cts = new();

        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = 64,
            CancellationToken = cts.Token,
        };

        int total = runtime.Context.Paths.Count;
        int done = 0;
        int lastPercent = -1;
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            await Parallel.ForEachAsync(
                runtime.Context.Paths,
                options,
                async (path, ct) =>
                {
                    try
                    {
                        MediaCacheEntry? cache = await runtime.MediaData.GetCache(path);
                        bool existsSource = await runtime.MediaData.Exists(path);
                        bool existsTarget = await runtime.MediaBackupData.Exists(path);

                        MediaBackupDirectPathScanResult result =
                            runtime.Dependencies.DirectPathScanOrchestrationService.Evaluate(
                                runtime.Dependencies.PathObservationCompositionService.BuildDirectPathObservation(
                                    new MediaBackupDirectPathObservationInput
                                    {
                                        Path = path,
                                        CacheExists = cache is not null,
                                        CachePath = cache?.Path,
                                        FileSizeBytes = cache?.Size?.File,
                                        SourceExists = existsSource,
                                        TargetExists = existsTarget,
                                        MaxPathSizeBytes = runtime.Config.Chunk.Path.Size,
                                    }
                                )
                            );

                        if (result.ShouldThrowMissingSource)
                            throw new InvalidOperationException(
                                $"source media missing for path {path}"
                            );

                        if (!result.ShouldIncludeDirectPath)
                            return;

                        runtime.Context.PathsDirect.Add(result.IncludedPath);
                    }
                    catch (OperationCanceledException)
                    {
                        runtime.Logger.LogWarning("Canceled {path}", path);
                    }
                    catch (Exception ex)
                    {
                        runtime.Logger.LogError(ex, "error in {path}: {error}", path, ex.Message);
                    }
                    finally
                    {
                        int current = Interlocked.Increment(ref done);
                        int prev = Volatile.Read(ref lastPercent);
                        MediaBackupProgressDecision progress =
                            runtime.Dependencies.ProgressPolicyService.Evaluate(
                                current,
                                total,
                                prev
                            );

                        if (progress.ShouldLog)
                        {
                            if (
                                Interlocked.CompareExchange(ref lastPercent, progress.Percent, prev)
                                == prev
                            )
                            {
                                runtime.Logger.LogInformation(
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

        List<string> pathsInChunks = runtime
            .Context.Chunks.Values.SelectMany(o => o.Data)
            .Select(o => o.Path)
            .ToList();

        MediaBackupDirectPathFinalizeResult finalize =
            runtime.Dependencies.DirectPathFinalizeService.Finalize(
                pathsInChunks,
                runtime.Context.PathsDirect
            );

        runtime.Context.PathsInBoth = finalize.PathsInBoth.ToList();

        runtime.Logger.LogInformation("{paths} in both", runtime.Context.PathsInBoth.Count);

        runtime.Context.PathsDirect = [.. finalize.DirectPaths];

        runtime.Logger.LogInformation(
            "{paths} paths > {size}",
            runtime.Context.PathsDirect.Count,
            runtime.Config.Chunk.Path.Size
        );
    }
}
