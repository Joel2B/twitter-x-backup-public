using Backup.Application.Media.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenancePrunePathSelectionService
    : IMediaMaintenancePrunePathSelectionService
{
    public IReadOnlySet<string> SelectPaths(IEnumerable<MediaDownload> downloads) =>
        downloads
            .SelectMany(download => download.Data.Select(item => item.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
