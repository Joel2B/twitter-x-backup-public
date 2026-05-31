using Backup.Infrastructure.Logging;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Media.Services;

public partial class MediaBackup
{
    private async Task Apply()
    {
        foreach (var kvp in _chunks)
        {
            IZipWriter? zip = null;

            try
            {
                if (_stop)
                    break;

                HashSet<string>? storagePaths = null;

                foreach (ChunkData chunkData in kvp.Value.Data)
                {
                    if (chunkData.Hash is not null)
                        continue;

                    chunkData.Hash = await MediaData.GetHash(
                        UtilsPath.NormalizePath(chunkData.Path)
                    );

                    if (chunkData.Hash is null)
                    {
                        _logger.LogInfo("error in hash: {path}", chunkData.Path);
                        continue;
                    }

                    if (zip is null)
                    {
                        _logger.LogInformation("processing chunk {chunk}", kvp.Key);
                        _logger.LogInfo("update zip");
                        zip = await OpenChunkZipWrite(kvp.Value, "apply");

                        if (zip is null)
                            break;
                    }

                    if (storagePaths is null)
                    {
                        _logger.LogInfo("reading entries");
                        storagePaths = [.. zip.GetEntries().Select(o => o.FullName)];
                    }

                    string relativePath = chunkData.Path.Replace('\\', '/');

                    if (storagePaths.TryGetValue(relativePath, out var _))
                        continue;

                    storagePaths.Add(relativePath);

                    await using Stream read = await MediaData.Read(
                        UtilsPath.NormalizePath(chunkData.Path)
                    );

                    await zip.AddEntry(relativePath, read);
                }

                if (zip is null || storagePaths is null)
                    continue;

                List<string> memory = [.. kvp.Value.Data.Select(o => o.Path.Replace('\\', '/'))];
                MediaBackupChunkReconciliationResult reconciliation =
                    _mediaBackupChunkReconciliationService.Reconcile(memory, storagePaths);
                int missing = reconciliation.MissingCount;
                IReadOnlyList<string> extras = reconciliation.ExtraPaths;

                _logger.LogInformation(
                    "{memory}/{storage}:{missing}/{extras}",
                    memory.Count,
                    storagePaths.Count,
                    missing,
                    extras.Count()
                );

                if (reconciliation.ShouldFail)
                    throw new Exception();

                if (extras.Any())
                {
                    foreach (string path in extras)
                        zip.RemoveEntry(path);

                    _logger.LogInformation("{extras} paths removed in storage", extras.Count());
                }

                if (!_backup.Chunks.Ids.Contains(kvp.Key))
                    _backup.Chunks.Ids.Add(kvp.Key);

                await _mediaBackupData.SaveBackup(_backup);
                await _mediaBackupData.Save([kvp.Value]);

                _logger.LogInformation("chunk {chunk} processed", kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error: {error}", JsonConvert.SerializeObject(ex));

                zip?.Dispose();

                await _mediaBackupData.DeleteChunk(kvp.Value);

                foreach (ChunkData chunkData in kvp.Value.Data)
                    chunkData.Hash = null;

                await _mediaBackupData.Save([kvp.Value]);

                break;
            }
            finally
            {
                zip?.Dispose();
            }
        }

        await ShowInfoChunks();
    }

    private async Task SyncChunks()
    {
        List<MediaBackupChunkPathsState> chunkStates = _chunks
            .Values.Select(chunk => new MediaBackupChunkPathsState
            {
                Id = chunk.Id,
                Paths = chunk.Data.Select(data => data.Path).ToList(),
            })
            .ToList();

        MediaBackupChunkSyncPlan plan = _mediaBackupChunkSyncPlanningService.Plan(
            chunkStates,
            _pathsInBoth
        );

        foreach (MediaBackupChunkSyncChunkPlan chunkPlan in plan.Chunks)
        {
            IZipWriter? zip = null;

            try
            {
                if (_stop)
                    break;

                foreach (string path in chunkPlan.PathsToRemove)
                {
                    if (zip is null)
                    {
                        _logger.LogInformation("processing chunk {chunk}", chunkPlan.ChunkId);
                        _logger.LogInfo("update zip");
                        zip = await OpenChunkZipWrite(_chunks[chunkPlan.ChunkId], "sync-chunks");

                        if (zip is null)
                            break;
                    }

                    _logger.LogInfo("removing entry", path);
                    zip.RemoveEntry(path.Replace('\\', '/'));
                    _logger.LogInfo("entry removed");

                    _chunks[chunkPlan.ChunkId].Data.RemoveAll(data => data.Path == path);
                }

                if (zip is null)
                    continue;

                await _mediaBackupData.Save([_chunks[chunkPlan.ChunkId]]);
                _logger.LogInformation("chunk {chunk} processed", chunkPlan.ChunkId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error: {error}", JsonConvert.SerializeObject(ex));

                zip?.Dispose();

                break;
            }
            finally
            {
                zip?.Dispose();
            }
        }

        foreach (string path in plan.DirectPathsToAdd)
            _pathsDirect.Add(path);
    }

    private async Task ApplyDirect()
    {
        await SyncChunks();

        CancellationTokenSource cts = new();

        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = 16,
            CancellationToken = cts.Token,
        };

        try
        {
            await Parallel.ForEachAsync(
                _pathsDirect,
                options,
                async (path, ct) =>
                {
                    try
                    {
                        bool cancel = false;

                        if (cancel)
                        {
                            cts.Cancel();
                            return;
                        }

                        await using Stream read = await MediaData.Read(
                            UtilsPath.NormalizePath(path)
                        );

                        await using Stream write = await _mediaBackupData.Write(
                            UtilsPath.NormalizePath(path)
                        );

                        await read.CopyToAsync(write, ct);
                        _logger.LogInfo("{path} path copied", path);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Canceled {path}", path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "error in {path}: {error}", path, ex.Message);
                    }
                }
            );
        }
        catch (OperationCanceledException) { }
    }
}
