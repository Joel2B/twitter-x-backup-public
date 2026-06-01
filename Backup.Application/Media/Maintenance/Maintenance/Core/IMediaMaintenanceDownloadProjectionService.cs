using Backup.Application.Media.Maintenance.Models;
using Backup.Application.Media.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenanceDownloadProjectionService
{
    IReadOnlyList<MediaMaintenanceCachedDownload> ToCachedDownloads(
        IEnumerable<MediaDownload> downloads,
        IReadOnlyDictionary<string, long?> cacheFileSizesByPath
    );

    IReadOnlyList<MediaDownload> ToDownloads(
        IEnumerable<MediaMaintenanceCachedDownload> cachedDownloads
    );
}
