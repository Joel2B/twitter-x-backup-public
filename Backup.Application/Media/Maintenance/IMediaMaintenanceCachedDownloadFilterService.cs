using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenanceCachedDownloadFilterService
{
    IReadOnlyList<MediaMaintenanceCachedDownload> Filter(
        IEnumerable<MediaMaintenanceCachedDownload> downloads
    );
}
