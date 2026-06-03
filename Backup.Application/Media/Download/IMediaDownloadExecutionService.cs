using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;

namespace Backup.Application.Media;

public interface IMediaDownloadExecutionService
{
    Task Run(
        IMediaDownloadExecutionCommand command,
        IMediaDownloadParallelRunner runner,
        IReadOnlyList<MediaDownloadQueueItem> queue,
        MediaParallelDownloadSettings settings,
        CancellationToken cancellationToken = default
    );
}
