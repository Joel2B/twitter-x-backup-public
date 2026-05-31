namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaMaintenanceCachedDownloadData
{
    public string Url { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public long? CacheFileSize { get; init; }
}
