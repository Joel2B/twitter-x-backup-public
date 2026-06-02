namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaMaintenanceCachedDownload
{
    public string Id { get; init; } = string.Empty;
    public IReadOnlyList<MediaMaintenanceCachedDownloadData> Data { get; init; } = [];
}
