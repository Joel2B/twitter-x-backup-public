using System.Diagnostics;
using Backup.App.Extensions;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media;
using Backup.App.Models.Media.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Media;

public partial class MediaBackup : IMediaBackup
{
    private async Task Calculate()
    {
        await ShowInfoChunks();

        int pathsPerChunk = _paths.Count / _config.Chunk.Count;
        int increaseCount = Math.Max(_backup.Chunks.Path.Increase, _config.Chunk.Path.Increase);

        _logger.LogInformation(
            "paths/residue: {paths}/{residue}",
            _paths.Count,
            _paths.Count % _config.Chunk.Count
        );

        _logger.LogInformation(
            "pathsPerChunk/increase/total: {paths}/{increase}/{total}",
            pathsPerChunk,
            increaseCount,
            pathsPerChunk + increaseCount
        );

        _logger.LogInfo("cloning chunks");

        Dictionary<int, Chunk> _chunksClone = _chunks.ToDictionary(
            o => o.Key,
            o => o.Value.Clone()
        );

        if (_chunks.Count == 0)
            _chunks[0] = new() { Id = 0 };

        _logger.LogInfo("expanding chunks");

        Dictionary<string, ChunkData> data = _chunks
            .Values.SelectMany(chunk => chunk.Data)
            .ToDictionary(o => o.Path, o => o);

        for (int i = 0; i < _backup.Chunks.Total; i++)
            if (!_chunks.ContainsKey(i))
                _chunks.Add(i, new() { Id = i });

        int currentChunk =
            _chunks
                .Values.GroupBy(o => o.Id)
                .Select(o => new
                {
                    id = o.Key,
                    Size = o.SelectMany(o => o.Data).Sum(o => o.FileSize),
                })
                .MinBy(o => o.Size)
                ?.id ?? 0;

        _logger.LogInfo("current chunk: {chunk}", currentChunk);

        List<string> newPaths = [];

        foreach (string path in _paths)
        {
            Cache? cache = await _mediaData.GetCache(path);

            if (
                cache is null
                || data.ContainsKey(cache.Path)
                || cache.Size?.File > _config.Chunk.Path.Size
            )
                continue;

            while (_chunks[currentChunk].Data.Count >= (pathsPerChunk + increaseCount))
            {
                currentChunk++;

                if (currentChunk >= _config.Chunk.Count)
                {
                    currentChunk = 0;

                    var min = _chunks.MinBy(o => o.Value.Data.Count);
                    var max = _chunks.MaxBy(o => o.Value.Data.Count);

                    _logger.LogInformation(
                        "min: {min}/{minCount}, max: {max}/{maxCount}",
                        min.Key,
                        min.Value.Data.Count,
                        max.Key,
                        max.Value.Data.Count
                    );

                    if (min.Value.Data.Count != max.Value.Data.Count)
                        currentChunk = min.Key;
                }
            }

            _chunks[currentChunk].Data.Add(new() { Path = cache.Path });
            newPaths.Add(path);
        }

        int newPathsCount = 0;
        bool header = false;

        foreach (var kvp in _chunks)
        {
            _chunksClone.TryGetValue(kvp.Key, out Chunk? chunkBefore);

            int before = chunkBefore is null ? 0 : chunkBefore.Data.Count;
            int after = _chunks[kvp.Key].Data.Count;
            int diff = after - before;
            newPathsCount += diff;

            long sizeBefore = 0;
            long sizeAfter = 0;

            foreach (ChunkData chunkData in chunkBefore?.Data ?? [])
            {
                Cache? cache = await _mediaData.GetCache(chunkData.Path);

                if (cache is not null)
                    sizeBefore += cache.Size?.File ?? 0;
            }

            foreach (ChunkData chunkData in _chunks[kvp.Key].Data)
            {
                Cache? cache = await _mediaData.GetCache(chunkData.Path);

                if (cache is not null)
                    sizeAfter += cache.Size?.File ?? 0;
            }

            if (before != after)
            {
                if (!header)
                {
                    header = true;

                    _logger.LogInfo(
                        "{id,-3} {before,-6} {after,-6} {diff,-6} {sizeBefore} {sizeAfter}",
                        "id",
                        "before",
                        "after",
                        "diff",
                        "size before (GiB)",
                        "size after (GiB)"
                    );
                }

                _logger.LogInformation(
                    "{id,-3} {before,-6} {after,-6} {diff,-6} {sizeBefore,-17} {sizeAfter}",
                    kvp.Key,
                    before,
                    after,
                    diff,
                    Math.Round(sizeBefore / 1024m / 1024m / 1024m, 2, MidpointRounding.ToZero),
                    Math.Round(sizeAfter / 1024m / 1024m / 1024m, 2, MidpointRounding.ToZero)
                );
            }
        }

        _logger.LogInformation("{paths1}/{paths2} new paths", newPathsCount, newPaths.Count);
    }

    public async Task CalculateDirect()
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
                        bool cancel = false;

                        if (cancel)
                        {
                            cts.Cancel();
                            return;
                        }

                        Cache? cache = await _mediaData.GetCache(path);

                        if (cache is null || (cache.Size?.File <= _config.Chunk.Path.Size))
                            return;

                        bool existsSource = await _mediaData.Exists(path);

                        if (!existsSource)
                            throw new Exception();

                        bool existsTarget = await _mediaBackup.Exists(path);

                        if (existsTarget)
                            return;

                        _pathsDirect.Add(cache.Path);
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
                            if (Interlocked.CompareExchange(ref lastPercent, percent, prev) == prev)
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

        _pathsInBoth = pathsInChunks.Intersect(_pathsDirect).ToList();

        _logger.LogInformation("{paths} in both", _pathsInBoth.Count);

        _pathsDirect = [.. _pathsDirect.ToList().Except(_pathsInBoth)];

        _logger.LogInformation(
            "{paths} paths > {size}",
            _pathsDirect.Count,
            _config.Chunk.Path.Size
        );
    }
}
