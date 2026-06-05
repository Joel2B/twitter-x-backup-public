using System.Diagnostics;
using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupDirectPathScanCoordinator(
    IMediaBackupDirectPathFinalizeService directPathFinalizeService
)
{
    private readonly IMediaBackupDirectPathFinalizeService _directPathFinalizeService =
        directPathFinalizeService;

    public async Task Scan(
        MediaBackupRuntime runtime,
        CancellationToken cancellationToken = default
    )
    {
        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = 64,
            CancellationToken = cancellationToken,
        };

        int total = runtime.Context.Paths.Count;
        int done = 0;
        int lastPercent = -1;
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            await Parallel.ForEachAsync(
                runtime.Context.Paths,
                options,
                async (path, ct) =>
                {
                    try
                    {
                        MediaCacheEntry? cache = await runtime.MediaData.GetCache(path);

                        if (
                            cache is null
                            || cache.Size?.File is not long fileSize
                            || fileSize <= runtime.Config.Chunk.Path.Size
                        )
                            return;

                        bool existsSource = await runtime.MediaData.Exists(path);

                        if (!existsSource)
                            throw new InvalidOperationException(
                                $"source media missing for path {path}"
                            );

                        bool existsTarget = await runtime.MediaBackupData.Exists(path);

                        if (existsTarget || string.IsNullOrWhiteSpace(cache.Path))
                            return;

                        runtime.Context.PathsDirect.Add(cache.Path);
                    }
                    catch (OperationCanceledException)
                    {
                        runtime.Logger.LogWarning("Canceled {path}", path);
                    }
                    catch (Exception ex)
                    {
                        runtime.Logger.LogError(ex, "error in {path}: {error}", path, ex.Message);
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

        MediaBackupDirectPathFinalizeResult finalize = _directPathFinalizeService.Finalize(
            pathsInChunks,
            runtime.Context.PathsDirect
        );

        runtime.Context.PathsInBoth = finalize.PathsInBoth.ToList();
        runtime.Logger.LogInfo("{paths} in both", runtime.Context.PathsInBoth.Count);

        runtime.Context.PathsDirect = [.. finalize.DirectPaths];
        runtime.Logger.LogInfo(
            "{paths} paths > {size}",
            runtime.Context.PathsDirect.Count,
            runtime.Config.Chunk.Path.Size
        );
    }

    private static int CalculateProgressPercent(int current, int total)
    {
        int safeTotal = Math.Max(total, 1);
        return (int)((long)current * 100 / safeTotal);
    }
}
