using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenanceIntegrityObservationCompositionService
{
    IReadOnlyList<MediaMaintenanceIntegrityProbeItem> BuildProbeItems(
        IReadOnlyList<MediaMaintenanceIntegrityTarget> targets,
        IReadOnlyDictionary<string, long?> cacheSizesByPath
    );

    IReadOnlyList<MediaMaintenanceIntegrityObservation> ToObservations(
        IReadOnlyList<MediaMaintenanceIntegrityProbeOutcome> outcomes
    );
}
