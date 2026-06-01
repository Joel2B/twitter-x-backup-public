using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenanceIntegrityBatchService
{
    MediaMaintenanceIntegrityBatchResult Evaluate(
        IReadOnlyList<MediaMaintenanceIntegrityObservation> observations
    );
}
