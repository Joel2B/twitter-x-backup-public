using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupSyncFinalizeService(
    IMediaBackupChunkSyncPlanningService chunkSyncPlanningService,
    IMediaBackupDirectPathQueueService directPathQueueService
) : IMediaBackupSyncFinalizeService
{
    private readonly IMediaBackupChunkSyncPlanningService _chunkSyncPlanningService =
        chunkSyncPlanningService;
    private readonly IMediaBackupDirectPathQueueService _directPathQueueService =
        directPathQueueService;

    public MediaBackupSyncFinalizeResult Finalize(
        IReadOnlyList<MediaBackupSyncFinalizeInputChunk> chunks,
        IReadOnlyList<string> pathsInBoth,
        IEnumerable<string> currentDirectPaths
    )
    {
        IReadOnlyList<MediaBackupChunkPathsState> states = chunks
            .Select(chunk => new MediaBackupChunkPathsState
            {
                Id = chunk.ChunkId,
                Paths = chunk.Paths.ToList(),
            })
            .ToList();

        MediaBackupChunkSyncPlan plan = _chunkSyncPlanningService.Plan(states, pathsInBoth);
        IReadOnlyList<string> merged = _directPathQueueService.MergeAndNormalize(
            currentDirectPaths,
            plan.DirectPathsToAdd
        );

        return new MediaBackupSyncFinalizeResult { Plan = plan, MergedDirectPaths = merged };
    }
}
