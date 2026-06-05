using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupChunkSyncMutationCoordinator
{
    public async Task<bool> Execute(
        MediaBackupRuntime runtime,
        MediaBackupChunkSyncChunkPlan chunkPlan,
        CancellationToken cancellationToken = default
    )
    {
        if (chunkPlan.PathsToRemove.Count == 0)
            return true;

        try
        {
            runtime.Logger.LogInformation("processing chunk {chunk}", chunkPlan.ChunkId);
            runtime.Logger.LogInfo("update zip");

            bool mutated = await runtime.MutateChunkZip(
                runtime.Context.Chunks[chunkPlan.ChunkId],
                "sync-chunks",
                zip =>
                {
                    foreach (string path in chunkPlan.PathsToRemove)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        runtime.Logger.LogInfo("removing entry", path);
                        zip.RemoveEntry(MediaBackupPathProjection.ToArchivePath(path));
                        runtime.Logger.LogInfo("entry removed");

                        runtime
                            .Context.Chunks[chunkPlan.ChunkId]
                            .Data.RemoveAll(data => data.Path == path);
                    }

                    return Task.CompletedTask;
                }
            );

            if (!mutated)
                return true;

            await runtime.MediaBackupData.Save([runtime.Context.Chunks[chunkPlan.ChunkId]]);
            runtime.Logger.LogInformation("chunk {chunk} processed", chunkPlan.ChunkId);
            return true;
        }
        catch (Exception ex)
        {
            runtime.Logger.LogError(ex, "error syncing backup chunk {chunk}", chunkPlan.ChunkId);
            return false;
        }
    }
}
