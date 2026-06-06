using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupApplyPhase(
    IMediaBackupChunkEntryStateService chunkEntryStateService,
    IMediaBackupChunkSyncPlanningService chunkSyncPlanningService,
    IMediaBackupDirectPathQueueService directPathQueueService,
    IMediaBackupChunkRuntimeCompositionService chunkRuntimeCompositionService,
    MediaBackupApplyChunkCoordinator mediaBackupApplyChunkCoordinator,
    MediaBackupChunkSyncMutationCoordinator mediaBackupChunkSyncMutationCoordinator
) : IMediaBackupApplyPhase
{
    private readonly IMediaBackupChunkEntryStateService _chunkEntryStateService =
        chunkEntryStateService;
    private readonly IMediaBackupChunkSyncPlanningService _chunkSyncPlanningService =
        chunkSyncPlanningService;
    private readonly IMediaBackupDirectPathQueueService _directPathQueueService =
        directPathQueueService;
    private readonly IMediaBackupChunkRuntimeCompositionService _chunkRuntimeCompositionService =
        chunkRuntimeCompositionService;
    private readonly MediaBackupApplyChunkCoordinator _mediaBackupApplyChunkCoordinator =
        mediaBackupApplyChunkCoordinator;
    private readonly MediaBackupChunkSyncMutationCoordinator _mediaBackupChunkSyncMutationCoordinator =
        mediaBackupChunkSyncMutationCoordinator;

    public async Task Apply(
        MediaBackupRuntime runtime,
        string? backupId,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (KeyValuePair<int, Chunk> kvp in runtime.Context.Chunks)
        {
            if (runtime.Stop)
                break;

            cancellationToken.ThrowIfCancellationRequested();

            bool shouldContinue = await _mediaBackupApplyChunkCoordinator.Execute(
                runtime,
                kvp.Key,
                kvp.Value,
                cancellationToken
            );

            if (!shouldContinue)
                break;
        }

        await runtime.ShowInfoChunks(backupId);
    }

    public async Task ApplyDirect(
        MediaBackupRuntime runtime,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await SyncChunks(runtime, cancellationToken);
        IReadOnlyList<string> directPaths = _directPathQueueService.Normalize(
            runtime.Context.PathsDirect
        );

        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = 16,
            CancellationToken = cancellationToken,
        };

        try
        {
            await Parallel.ForEachAsync(
                directPaths,
                options,
                async (path, ct) =>
                {
                    try
                    {
                        await using Stream read = await runtime.MediaData.Read(
                            UtilsPath.NormalizePath(path)
                        );
                        await using Stream write = await runtime.MediaBackupData.Write(
                            UtilsPath.NormalizePath(path)
                        );

                        await read.CopyToAsync(write, ct);
                        runtime.Logger.LogInfo("{path} path copied", path);
                    }
                    catch (OperationCanceledException)
                    {
                        runtime.Logger.LogWarning("Canceled {path}", path);
                    }
                    catch (Exception ex)
                    {
                        runtime.Logger.LogError(ex, "error in {path}: {error}", path, ex.Message);
                    }
                }
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task SyncChunks(
        MediaBackupRuntime runtime,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MediaBackupSyncFinalizeInputChunk> chunkStates =
            _chunkEntryStateService.BuildSyncFinalizeInputChunks(
                _chunkRuntimeCompositionService.BuildChunkPathStates(
                    runtime.Context.Chunks.Values.Select(chunk => new MediaBackupChunkPathsInput
                    {
                        Id = chunk.Id,
                        Paths = chunk.Data.Select(data => data.Path).ToList(),
                    })
                )
            );
        IReadOnlyList<MediaBackupChunkPathsState> states = chunkStates
            .Select(chunk => new MediaBackupChunkPathsState
            {
                Id = chunk.ChunkId,
                Paths = chunk.Paths.ToList(),
            })
            .ToList();

        MediaBackupChunkSyncPlan plan = _chunkSyncPlanningService.Plan(
            states,
            runtime.Context.PathsInBoth
        );

        foreach (MediaBackupChunkSyncChunkPlan chunkPlan in plan.Chunks)
        {
            if (runtime.Stop)
                break;

            cancellationToken.ThrowIfCancellationRequested();

            bool shouldContinue = await _mediaBackupChunkSyncMutationCoordinator.Execute(
                runtime,
                chunkPlan,
                cancellationToken
            );

            if (!shouldContinue)
                break;
        }

        runtime.Context.PathsDirect =
        [
            .. _directPathQueueService.MergeAndNormalize(
                runtime.Context.PathsDirect,
                plan.DirectPathsToAdd
            ),
        ];
    }
}
