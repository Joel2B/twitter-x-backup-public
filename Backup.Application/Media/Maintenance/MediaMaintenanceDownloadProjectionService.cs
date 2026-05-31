using Backup.Application.Media.Maintenance.Models;
using Backup.Application.Media.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenanceDownloadProjectionService
    : IMediaMaintenanceDownloadProjectionService
{
    public IReadOnlyList<MediaMaintenanceCachedDownload> ToCachedDownloads(
        IEnumerable<MediaDownload> downloads,
        IReadOnlyDictionary<string, long?> cacheFileSizesByPath
    ) =>
        downloads
            .Select(download => new MediaMaintenanceCachedDownload
            {
                Id = download.Id,
                Data = download
                    .Data.Select(data => new MediaMaintenanceCachedDownloadData
                    {
                        Url = data.Url,
                        Path = data.Path,
                        CacheFileSize = cacheFileSizesByPath.TryGetValue(data.Path, out long? size)
                            ? size
                            : null,
                    })
                    .ToList(),
            })
            .ToList();

    public IReadOnlyList<MediaDownload> ToDownloads(
        IEnumerable<MediaMaintenanceCachedDownload> cachedDownloads
    ) =>
        cachedDownloads
            .Select(download => new MediaDownload
            {
                Id = download.Id,
                Data = download
                    .Data.Select(data => new MediaDownloadData { Url = data.Url, Path = data.Path })
                    .ToList(),
            })
            .ToList();
}
