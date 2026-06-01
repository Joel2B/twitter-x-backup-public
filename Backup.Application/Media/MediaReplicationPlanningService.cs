using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public sealed class MediaReplicationPlanningService : IMediaReplicationPlanningService
{
    public IReadOnlyList<MediaReplicationCopyAction> SelectCopyActions(
        IEnumerable<MediaReplicationPathObservation> observations
    ) =>
        observations
            .Where(item => item.ExistsInSource && !item.ExistsInTarget)
            .Select(item => new MediaReplicationCopyAction
            {
                DownloadId = item.DownloadId,
                Url = item.Url,
                Path = item.Path,
            })
            .ToList();

    public IReadOnlyList<MediaDownload> RemoveCopied(
        IEnumerable<MediaDownload> downloads,
        IEnumerable<MediaReplicationCopyAction> copied
    )
    {
        HashSet<(string DownloadId, string Path)> copiedKeys =
        [
            .. copied.Select(item => (item.DownloadId, item.Path)),
        ];

        if (copiedKeys.Count == 0)
            return downloads.Select(item => item.Clone()).ToList();

        return downloads
            .Select(download =>
            {
                List<MediaDownloadData> remaining = download
                    .Data.Where(item => !copiedKeys.Contains((download.Id, item.Path)))
                    .Select(item => item.Clone())
                    .ToList();

                return new MediaDownload { Id = download.Id, Data = remaining };
            })
            .Where(download => download.Data.Count > 0)
            .ToList();
    }
}
