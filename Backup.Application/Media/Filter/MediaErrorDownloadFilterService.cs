using Backup.Application.Media.Models;

namespace Backup.Application.Media.Filter;

public sealed class MediaErrorDownloadFilterService : IMediaErrorDownloadFilterService
{
    public IReadOnlyList<MediaDownload> FilterByExcludedUrls(
        IReadOnlyList<MediaDownload> downloads,
        IReadOnlySet<string> excludedUrls
    ) =>
        downloads
            .Select(download => new MediaDownload
            {
                Id = download.Id,
                Data = download
                    .Data.Where(data => !excludedUrls.Contains(data.Url))
                    .Select(data => new MediaDownloadData { Url = data.Url, Path = data.Path })
                    .ToList(),
            })
            .Where(download => download.Data.Count > 0)
            .ToList();
}
