using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public interface IMediaDownloadQueueBuilderService
{
    IReadOnlyList<MediaDownloadQueueItem> Build(
        IEnumerable<MediaDownloadQueueItem> items,
        int maxCount
    );
}
