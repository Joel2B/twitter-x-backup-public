using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenanceCachedDownloadFilterService(
    IMediaMaintenanceDataPolicyService mediaMaintenanceDataPolicyService
) : IMediaMaintenanceCachedDownloadFilterService
{
    private readonly IMediaMaintenanceDataPolicyService _mediaMaintenanceDataPolicyService =
        mediaMaintenanceDataPolicyService;

    public IReadOnlyList<MediaMaintenanceCachedDownload> Filter(
        IEnumerable<MediaMaintenanceCachedDownload> downloads
    ) =>
        downloads
            .Select(download => new MediaMaintenanceCachedDownload
            {
                Id = download.Id,
                Data = download
                    .Data.Where(data =>
                        !_mediaMaintenanceDataPolicyService.ShouldRemoveCachedDownload(
                            data.CacheFileSize
                        )
                    )
                    .ToList(),
            })
            .Where(download => download.Data.Count > 0)
            .ToList();
}
