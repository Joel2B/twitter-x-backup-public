using Backup.Application.Media.Maintenance.Models;
using Backup.Application.Media.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenanceIntegrityTargetService
{
    IReadOnlyList<MediaMaintenanceIntegrityTarget> BuildTargets(
        IReadOnlyList<MediaDownload> downloads
    );

    IReadOnlyList<MediaDownload> RemoveByCorrelations(
        IReadOnlyList<MediaDownload> downloads,
        IReadOnlySet<string> removeCorrelations
    );
}
