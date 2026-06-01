using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Utils;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupApplyPhase : IMediaBackupApplyPhase
{
    public async Task Apply(MediaBackupRuntime runtime, string? backupId)
    {
        foreach (KeyValuePair<int, Chunk> kvp in runtime.Context.Chunks)
        {
            IZipWriter? zip = null;

            try
            {
                if (runtime.Stop)
                    break;

                IReadOnlyList<MediaBackupChunkEntryState> initialEntryStates =
                    runtime.BuildChunkEntryStates(kvp.Value.Data);
                IReadOnlyList<string> pathsNeedingHash =
                    runtime.Dependencies.ChunkHashPreparationService.SelectPathsNeedingHash(
                        initialEntryStates
                    );
                Dictionary<string, string?> hashByPath = new(StringComparer.Ordinal);

                foreach (string path in pathsNeedingHash)
                {
                    string? hash = await runtime.MediaData.GetHash(UtilsPath.NormalizePath(path));
                    hashByPath[path] = hash;

                    if (hash is null)
                    {
                        runtime.Logger.LogInfo("error in hash: {path}", path);
                    }
                }

                IReadOnlyList<MediaBackupChunkEntryState> hashedEntryStates =
                    runtime.Dependencies.ChunkHashPreparationService.ApplyHashes(
                        initialEntryStates,
                        hashByPath
                    );
                runtime.ApplyChunkEntryStates(kvp.Value, hashedEntryStates);

                IReadOnlyList<MediaBackupApplyChunkPathState> chunkPaths =
                    runtime.Dependencies.ChunkEntryStateService.BuildApplyChunkPathStates(
                        hashedEntryStates
                    );

                if (!chunkPaths.Any(item => item.HasHash))
                    continue;

                runtime.Logger.LogInformation("processing chunk {chunk}", kvp.Key);
                runtime.Logger.LogInfo("update zip");
                zip = await runtime.OpenChunkZipWrite(kvp.Value, "apply");

                if (zip is null)
                    continue;

                runtime.Logger.LogInfo("reading entries");
                HashSet<string> storagePaths = [.. zip.GetEntries().Select(o => o.FullName)];
                MediaBackupApplyChunkPlan chunkPlan = runtime.Dependencies.ApplyChunkPlanningService.Plan(
                    chunkPaths,
                    storagePaths,
                    runtime.Context.Backup.Chunks.Ids,
                    kvp.Key
                );

                if (!chunkPlan.ShouldProcessChunk || chunkPlan.FinalizePlan is null)
                    continue;

                foreach (MediaBackupApplyEntryCandidate item in chunkPlan.EntriesToAdd)
                {
                    storagePaths.Add(item.ArchivePath);
                    await using Stream read = await runtime.MediaData.Read(
                        UtilsPath.NormalizePath(item.SourcePath)
                    );
                    await zip.AddEntry(item.ArchivePath, read);
                }

                MediaBackupApplyFinalizePlan finalizePlan = chunkPlan.FinalizePlan;
                MediaBackupStorageConsistencyDecision decision = finalizePlan.ConsistencyDecision;

                runtime.Logger.LogInformation(
                    "{memory}/{storage}:{missing}/{extras}",
                    kvp.Value.Data.Count,
                    storagePaths.Count,
                    decision.MissingCount,
                    decision.ExtraPaths.Count
                );

                if (decision.ShouldFail)
                    throw new InvalidOperationException(
                        $"backup apply consistency check failed for chunk {kvp.Key}"
                    );

                if (decision.ShouldRemoveExtras)
                {
                    foreach (string path in decision.ExtraPaths)
                        zip.RemoveEntry(path);

                    runtime.Logger.LogInformation(
                        "{extras} paths removed in storage",
                        decision.ExtraPaths.Count
                    );
                }

                runtime.Context.Backup.Chunks.Ids = finalizePlan.ChunkIds.ToList();

                await runtime.MediaBackupData.SaveBackup(runtime.Context.Backup);
                await runtime.MediaBackupData.Save([kvp.Value]);

                runtime.Logger.LogInformation("chunk {chunk} processed", kvp.Key);
            }
            catch (Exception ex)
            {
                runtime.Logger.LogError(ex, "error applying backup chunk {chunk}", kvp.Key);

                await runtime.MediaBackupData.DeleteChunk(kvp.Value);

                IReadOnlyList<MediaBackupChunkEntryState> resetStates =
                    runtime.Dependencies.ChunkFailureApplyService.ApplyForApplyFailure(
                        runtime.BuildChunkEntryStates(kvp.Value.Data)
                    );
                runtime.ApplyChunkEntryStates(kvp.Value, resetStates);

                await runtime.MediaBackupData.Save([kvp.Value]);

                break;
            }
            finally
            {
                zip?.Dispose();
            }
        }

        await runtime.ShowInfoChunks(backupId);
    }

    public async Task ApplyDirect(MediaBackupRuntime runtime)
    {
        await SyncChunks(runtime);
        IReadOnlyList<string> directPaths = runtime.Dependencies.DirectApplyPathService.GetPaths(
            runtime.Context.PathsDirect
        );

        CancellationTokenSource cts = new();

        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = 16,
            CancellationToken = cts.Token,
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
        catch (OperationCanceledException) { }
    }

    private static async Task SyncChunks(MediaBackupRuntime runtime)
    {
        IReadOnlyList<MediaBackupSyncFinalizeInputChunk> chunkStates =
            runtime.Dependencies.ChunkEntryStateService.BuildSyncFinalizeInputChunks(
                runtime.Dependencies.ChunkRuntimeCompositionService.BuildChunkPathStates(
                    runtime.Context.Chunks.Values.Select(chunk => new MediaBackupChunkPathsInput
                    {
                        Id = chunk.Id,
                        Paths = chunk.Data.Select(data => data.Path).ToList(),
                    })
                )
            );

        MediaBackupSyncFinalizeResult finalize = runtime.Dependencies.SyncFinalizeService.Finalize(
            chunkStates,
            runtime.Context.PathsInBoth,
            runtime.Context.PathsDirect
        );
        MediaBackupChunkSyncPlan plan = finalize.Plan;

        foreach (MediaBackupChunkSyncChunkPlan chunkPlan in plan.Chunks)
        {
            IZipWriter? zip = null;

            try
            {
                if (runtime.Stop)
                    break;

                foreach (string path in chunkPlan.PathsToRemove)
                {
                    if (zip is null)
                    {
                        runtime.Logger.LogInformation("processing chunk {chunk}", chunkPlan.ChunkId);
                        runtime.Logger.LogInfo("update zip");
                        zip = await runtime.OpenChunkZipWrite(
                            runtime.Context.Chunks[chunkPlan.ChunkId],
                            "sync-chunks"
                        );

                        if (zip is null)
                            break;
                    }

                    runtime.Logger.LogInfo("removing entry", path);
                    zip.RemoveEntry(runtime.Dependencies.PathProjectionService.ToArchivePath(path));
                    runtime.Logger.LogInfo("entry removed");

                    runtime.Context.Chunks[chunkPlan.ChunkId].Data.RemoveAll(data => data.Path == path);
                }

                if (zip is null)
                    continue;

                await runtime.MediaBackupData.Save([runtime.Context.Chunks[chunkPlan.ChunkId]]);
                runtime.Logger.LogInformation("chunk {chunk} processed", chunkPlan.ChunkId);
            }
            catch (Exception ex)
            {
                runtime.Logger.LogError(ex, "error syncing backup chunk {chunk}", chunkPlan.ChunkId);
                break;
            }
            finally
            {
                zip?.Dispose();
            }
        }

        runtime.Context.PathsDirect = [.. finalize.MergedDirectPaths];
    }
}
