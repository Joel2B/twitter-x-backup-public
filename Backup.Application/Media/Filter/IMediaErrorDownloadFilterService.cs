using Backup.Application.Media.Models;

namespace Backup.Application.Media.Filter;

public interface IMediaErrorDownloadFilterService
{
    IReadOnlyList<MediaDownload> FilterByExcludedUrls(
        IReadOnlyList<MediaDownload> downloads,
        IReadOnlySet<string> excludedUrls
    );
}
