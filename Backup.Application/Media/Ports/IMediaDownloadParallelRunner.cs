using Backup.Application.Media.Models;

namespace Backup.Application.Media.Ports;

public interface IMediaDownloadParallelRunner
{
    Task Run(
        IReadOnlyList<MediaDownloadQueueItem> queue,
        MediaParallelDownloadSettings settings,
        Func<MediaDownloadQueueItem, CancellationToken, Task> processItem,
        Action<string> debugSink,
        CancellationToken cancellationToken
    );
}
