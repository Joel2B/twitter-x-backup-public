using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Interfaces.Services.UtilsService;
using Backup.Infrastructure.Models.Media.Backup;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Services.Media;

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
                int missing = memory.Except(storagePaths).Count();
                IEnumerable<string> extras = storagePaths.Except(memory);

                _logger.LogInformation(
                    "{memory}/{storage}:{missing}/{extras}",
                    memory.Count,
                    storagePaths.Count,
                    missing,
                    extras.Count()
                );

                if (missing != 0)
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
        HashSet<string> pathsInBotSet = [.. _pathsInBoth];

        List<Chunk> chunks = _chunks
            .Values.Select(chunk => new Chunk
            {
                Id = chunk.Id,
                Data = chunk
                    .Data.Where(o => pathsInBotSet.Contains(o.Path))
                    .Select(o => o.Clone())
                    .ToList(),
            })
            .Where(o => o.Data.Count > 0)
            .ToList();

        foreach (Chunk chunk in chunks)
        {
            IZipWriter? zip = null;

            try
            {
                if (_stop)
                    break;

                foreach (ChunkData chunkData in chunk.Data)
                {
                    if (zip is null)
                    {
                        _logger.LogInformation("processing chunk {chunk}", chunk.Id);
                        _logger.LogInfo("update zip");
                        zip = await OpenChunkZipWrite(chunk, "sync-chunks");

                        if (zip is null)
                            break;
                    }

                    _logger.LogInfo("removing entry", chunkData.Path);
                    zip.RemoveEntry(chunkData.Path.Replace('\\', '/'));
                    _logger.LogInfo("entry removed");

                    _chunks[chunk.Id].Data.RemoveAll(data => data.Path == chunkData.Path);
                    _pathsDirect.Add(chunkData.Path);
                }

                if (zip is null)
                    continue;

                await _mediaBackupData.Save([_chunks[chunk.Id]]);
                _logger.LogInformation("chunk {chunk} processed", chunk.Id);
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

