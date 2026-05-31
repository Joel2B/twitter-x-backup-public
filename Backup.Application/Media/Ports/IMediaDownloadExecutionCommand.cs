using Backup.Application.Media.Models;

namespace Backup.Application.Media.Ports;

public interface IMediaDownloadExecutionCommand
{
    Task<Stream> Download(MediaDownloadQueueItem item, CancellationToken cancellationToken);

    Task Save(MediaDownloadQueueItem item, Stream stream, CancellationToken cancellationToken);

    void OnSuccess(MediaDownloadQueueItem item);

    void OnItemError(MediaDownloadQueueItem item, string message);

    bool ShouldCancelOnItemError(Exception exception);

    void OnFatalError(string message);

    void OnDebug(string message);

    Task SaveState();

    Task SaveLogs();
}
