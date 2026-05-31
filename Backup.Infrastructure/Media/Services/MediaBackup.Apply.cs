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
                }

                IReadOnlyList<MediaBackupApplyEntryCandidate> candidates = kvp.Value.Data
                    .Select(item => new MediaBackupApplyEntryCandidate
                    {
                        SourcePath = item.Path,
                        ArchivePath = _mediaBackupPathProjectionService.ToArchivePath(item.Path),
                        HasHash = item.Hash is not null,
                    })
                    .ToList();

                if (!candidates.Any(item => item.HasHash))
                    continue;

                _logger.LogInformation("processing chunk {chunk}", kvp.Key);
                _logger.LogInfo("update zip");
                zip = await OpenChunkZipWrite(kvp.Value, "apply");

                if (zip is null)
                    continue;

                _logger.LogInfo("reading entries");
                HashSet<string> storagePaths = [.. zip.GetEntries().Select(o => o.FullName)];

                IReadOnlyList<MediaBackupApplyEntryCandidate> toAdd =
                    _mediaBackupApplyEntrySelectionService.SelectEntriesToAdd(candidates, storagePaths);

                foreach (MediaBackupApplyEntryCandidate item in toAdd)
                {
                    storagePaths.Add(item.ArchivePath);
                    await using Stream read = await MediaData.Read(
                        UtilsPath.NormalizePath(item.SourcePath)
                    );
                    await zip.AddEntry(item.ArchivePath, read);
                }

                IReadOnlyList<string> memory = _mediaBackupPathProjectionService.ToArchivePaths(
                    kvp.Value.Data.Select(item => item.Path)
                );
                MediaBackupApplyFinalizePlan finalizePlan = _mediaBackupApplyFinalizeService.Plan(
                    memory,
                    storagePaths,
                    _backup.Chunks.Ids,
                    kvp.Key
                );
                MediaBackupStorageConsistencyDecision decision = finalizePlan.ConsistencyDecision;

                _logger.LogInformation(
                    "{memory}/{storage}:{missing}/{extras}",
                    memory.Count,
                    storagePaths.Count,
                    decision.MissingCount,
                    decision.ExtraPaths.Count
                );

                if (decision.ShouldFail)
                    throw new Exception();

                if (decision.ShouldRemoveExtras)
                {
                    foreach (string path in decision.ExtraPaths)
                        zip.RemoveEntry(path);

                    _logger.LogInformation(
                        "{extras} paths removed in storage",
                        decision.ExtraPaths.Count
                    );
                }

                _backup.Chunks.Ids = finalizePlan.ChunkIds.ToList();

                await _mediaBackupData.SaveBackup(_backup);
                await _mediaBackupData.Save([kvp.Value]);

                _logger.LogInformation("chunk {chunk} processed", kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error: {error}", JsonConvert.SerializeObject(ex));

                    zip?.Dispose();

                await _mediaBackupData.DeleteChunk(kvp.Value);

                IReadOnlyList<MediaBackupChunkFailureState> resetStates =
                    _mediaBackupChunkFailurePolicyService.ResetForApplyFailure(
                        kvp.Value.Data.Select(item => new MediaBackupChunkFailureState
                        {
                            Path = item.Path,
                            Hash = item.Hash,
                            FileSize = item.FileSize,
                            Crc32 = item.Crc32,
                        })
                    );

                ApplyFailureStates(kvp.Value, resetStates);

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
        List<MediaBackupSyncFinalizeInputChunk> chunkStates = _chunks
            .Values.Select(chunk => new MediaBackupChunkPathsState
            {
                Id = chunk.Id,
                Paths = chunk.Data.Select(data => data.Path).ToList(),
            })
            .Select(chunk => new MediaBackupSyncFinalizeInputChunk
            {
                ChunkId = chunk.Id,
                Paths = chunk.Paths,
            })
            .ToList();

        MediaBackupSyncFinalizeResult finalize = _mediaBackupSyncFinalizeService.Finalize(
            chunkStates,
            _pathsInBoth,
            _pathsDirect
        );
        MediaBackupChunkSyncPlan plan = finalize.Plan;

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
                    zip.RemoveEntry(_mediaBackupPathProjectionService.ToArchivePath(path));
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

        _pathsDirect = [.. finalize.MergedDirectPaths];
    }

    private async Task ApplyDirect()
    {
        await SyncChunks();
        IReadOnlyList<string> directPaths = _mediaBackupDirectApplyPathService.GetPaths(_pathsDirect);

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
