using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public sealed class MediaDuplicateFilterService : IMediaDuplicateFilterService
{
    public IReadOnlyList<MediaDownload> Filter(IReadOnlyList<MediaDownload> downloads)
    {
        HashSet<string> urls = new(StringComparer.OrdinalIgnoreCase);
        List<MediaDownload> filtered = [];

        foreach (MediaDownload download in downloads)
        {
            List<MediaDownloadData> data = download
                .Data.Where(item => urls.Add(item.Url))
                .Select(item => item.Clone())
                .ToList();

            if (data.Count == 0)
                continue;

            filtered.Add(new MediaDownload { Id = download.Id, Data = data });
        }

        return filtered;
    }
}
