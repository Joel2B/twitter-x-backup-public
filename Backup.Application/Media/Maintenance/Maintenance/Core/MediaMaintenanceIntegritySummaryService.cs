using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenanceIntegritySummaryService
    : IMediaMaintenanceIntegritySummaryService
{
    public MediaMaintenanceIntegritySummary Summarize(
        IEnumerable<MediaMaintenanceIntegrityEvaluation> evaluations
    )
    {
        int nullCount = 0;
        int sizeCount = 0;
        int invalidCount = 0;

        foreach (MediaMaintenanceIntegrityEvaluation evaluation in evaluations)
        {
            nullCount += evaluation.NullCountIncrement;
            sizeCount += evaluation.SizeCountIncrement;
            invalidCount += evaluation.InvalidCountIncrement;
        }

        return new MediaMaintenanceIntegritySummary
        {
            NullCount = nullCount,
            SizeCount = sizeCount,
            InvalidCount = invalidCount,
        };
    }
}
