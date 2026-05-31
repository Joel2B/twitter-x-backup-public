namespace Backup.Application.Media.Models;

public sealed class MediaDownloadQueueItem
{
    public required string DownloadId { get; init; }

    public required string Url { get; init; }

    public required string Path { get; init; }
}
