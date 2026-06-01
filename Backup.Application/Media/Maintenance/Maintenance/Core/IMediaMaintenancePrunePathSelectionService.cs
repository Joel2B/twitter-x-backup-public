using Backup.Application.Media.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenancePrunePathSelectionService
{
    IReadOnlySet<string> SelectPaths(IEnumerable<MediaDownload> downloads);
}
