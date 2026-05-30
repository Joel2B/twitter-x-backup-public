using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public interface IMediaDuplicateFilterService
{
    IReadOnlyList<MediaDownload> Filter(IReadOnlyList<MediaDownload> downloads);
}
