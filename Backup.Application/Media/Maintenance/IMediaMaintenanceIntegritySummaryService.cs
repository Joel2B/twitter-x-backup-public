using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenanceIntegritySummaryService
{
    MediaMaintenanceIntegritySummary Summarize(
        IEnumerable<MediaMaintenanceIntegrityEvaluation> evaluations
    );
}
