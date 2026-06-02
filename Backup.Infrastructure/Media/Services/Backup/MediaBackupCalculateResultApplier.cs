using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupCalculateResultApplier
{
    public void Apply(MediaBackupRuntime runtime, MediaBackupCalculateExecutionResult calculation)
    {
        MediaBackupChunkPlanningResult plan = calculation.Planning;

        runtime.Logger.LogInfo(
            "paths/residue: {paths}/{residue}",
            runtime.Context.Paths.Count,
            runtime.Context.Paths.Count % runtime.Config.Chunk.Count
        );

        runtime.Logger.LogInfo(
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
                runtime.Logger.LogInfo(
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

        runtime.Logger.LogInfo(
            "{paths1}/{paths2} new paths",
            deltaLogPlan.TotalAddedPaths,
            calculation.ApplyAssignments.AddedOriginalPaths.Count
        );
    }
}
