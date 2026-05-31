using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public sealed class MediaDownloadQueueBuilderService(
    IMediaDownloadPathPriorityPolicyService pathPriorityPolicyService
) : IMediaDownloadQueueBuilderService
{
    private readonly IMediaDownloadPathPriorityPolicyService _pathPriorityPolicyService =
        pathPriorityPolicyService;

    public IReadOnlyList<MediaDownloadQueueItem> Build(
        IEnumerable<MediaDownloadQueueItem> items,
        int maxCount
    )
    {
        IEnumerable<MediaDownloadQueueItem> ordered = items.OrderBy(item =>
            _pathPriorityPolicyService.GetPriority(item.Path)
        );

        if (maxCount >= 0)
            ordered = ordered.Take(maxCount);

        return ordered.ToList();
    }
}
