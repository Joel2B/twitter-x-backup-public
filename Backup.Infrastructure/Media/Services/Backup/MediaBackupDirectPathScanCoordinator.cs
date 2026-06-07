using System.Diagnostics;
using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupDirectPathScanCoordinator(
    IMediaBackupDirectPathQueueService directPathQueueService,
    IMediaBackupDirectPathSelectionService directPathSelectionService
)
{
    private readonly IMediaBackupDirectPathQueueService _directPathQueueService =
        directPathQueueService;
    private readonly IMediaBackupDirectPathSelectionService _directPathSelectionService =
        directPathSelectionService;

    public async Task Scan(
        MediaBackupRuntime runtime,
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyList<string> normalizedPaths = _directPathQueueService.Normalize(
            runtime.Context.Paths
        );

        List<DirectPath> paths = await BuildPaths(runtime, normalizedPaths, cancellationToken);

        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = 64,
            CancellationToken = cancellationToken,
        };

        int total = paths.Count;
        int done = 0;
        int lastPercent = -1;
        Stopwatch sw = Stopwatch.StartNew();

        runtime.Logger.LogInfo(
            "direct path scan: paths={count}, threshold={size}",
            total,
            runtime.Config.Chunk.Path.Size
        );

        try
        {
            await Parallel.ForEachAsync(
                paths,
                options,
                async (candidate, ct) =>
                {
                    try
                    {
                        bool existsSource = await runtime.MediaData.Exists(candidate.OriginalPath);

                        if (!existsSource)
                            throw new InvalidOperationException(
                                $"source media missing for path {candidate.OriginalPath}"
                            );

                        bool existsTarget = await runtime.MediaBackupData.Exists(
                            candidate.OriginalPath
                        );

                        if (existsTarget)
                            return;

                        runtime.Context.PathsDirect.Add(candidate.CachePath);
                    }
                    catch (OperationCanceledException)
                    {
                        runtime.Logger.LogWarning("Canceled {path}", candidate.OriginalPath);
                    }
                    catch (Exception ex)
                    {
                        runtime.Logger.LogError(
                            ex,
                            "error in {path}: {error}",
                            candidate.OriginalPath,
                            ex.Message
                        );
                    }
                    finally
                    {
                        int current = Interlocked.Increment(ref done);
                        int prev = Volatile.Read(ref lastPercent);
                        int percent = CalculateProgressPercent(current, total);
                        bool shouldLog = percent != prev;

                        if (shouldLog)
                        {
                            shouldLog =
                                Interlocked.CompareExchange(ref lastPercent, percent, prev) == prev;
                        }

                        if (shouldLog)
                        {
                            runtime.Logger.LogInfo(
                                "Progress: {percent}% ({current}/{total}) elapsed={elapsed}",
                                percent,
                                current,
                                total,
                                sw.Elapsed
                            );
                        }
                    }
                }
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }

        List<string> pathsInChunks = runtime
            .Context.Chunks.Values.SelectMany(chunk => chunk.Data)
            .Select(item => item.Path)
            .ToList();

        IReadOnlyList<string> normalizedDirectPaths = _directPathQueueService.Normalize(
            runtime.Context.PathsDirect
        );

        MediaBackupDirectPathSelectionResult selection = _directPathSelectionService.Select(
            pathsInChunks,
            normalizedDirectPaths
        );

        runtime.Context.PathsInBoth = selection.PathsInBoth.ToList();
        runtime.Logger.LogInfo("{paths} in both", runtime.Context.PathsInBoth.Count);

        runtime.Context.PathsDirect = [.. _directPathQueueService.Normalize(selection.DirectPaths)];
        runtime.Logger.LogInfo(
            "{paths} paths > {size}",
            runtime.Context.PathsDirect.Count,
            runtime.Config.Chunk.Path.Size
        );
    }

    private static async Task<List<DirectPath>> BuildPaths(
        MediaBackupRuntime runtime,
        IEnumerable<string> paths,
        CancellationToken cancellationToken
    )
    {
        List<DirectPath> directPaths = [];

        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MediaCacheEntry? cache = await runtime.MediaData.GetCache(path);

            if (cache is null || string.IsNullOrWhiteSpace(cache.Path))
                continue;

            if (cache.Size?.File is not long fileSize)
                continue;

            if (fileSize <= runtime.Config.Chunk.Path.Size)
                continue;

            directPaths.Add(new(path, cache.Path));
        }

        return directPaths;
    }

    private static int CalculateProgressPercent(int current, int total)
    {
        int safeTotal = Math.Max(total, 1);
        return (int)((long)current * 100 / safeTotal);
    }

    private sealed record DirectPath(string OriginalPath, string CachePath);
}
