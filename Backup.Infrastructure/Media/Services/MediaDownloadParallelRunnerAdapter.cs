using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;

namespace Backup.Infrastructure.Media.Services;

public sealed class MediaDownloadParallelRunnerAdapter : IMediaDownloadParallelRunner
{
    public Task Run(
        IReadOnlyList<MediaDownloadQueueItem> queue,
        MediaParallelDownloadSettings settings,
        Func<MediaDownloadQueueItem, CancellationToken, Task> processItem,
        Action<string> debugSink,
        CancellationToken cancellationToken
    )
    {
        DynamicParallelOptions options = new()
        {
            MinDegreeOfParallelism = settings.MinDegreeOfParallelism,
            MaxDegreeOfParallelism = settings.MaxDegreeOfParallelism,
            StartDegreeOfParallelism = settings.StartDegreeOfParallelism,
            TargetDuration = settings.TargetDuration,
            JumpToMaxOnFastAverage = settings.JumpToMaxOnFastAverage,
            EnableHeavyCut = settings.EnableHeavyCut,
            HeavyThreshold = settings.HeavyThreshold,
            CancellationToken = cancellationToken,
            EnableDebug = settings.EnableDebug,
            DebugSink = debugSink,
            StrictDecreaseGate = settings.StrictDecreaseGate,
        };

        return DynamicParallel.ForEachAsync(queue, options, processItem);
    }
}
