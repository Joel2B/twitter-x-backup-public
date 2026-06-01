using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenanceIntegrityBatchService(
    IMediaMaintenanceIntegrityDecisionService mediaMaintenanceIntegrityDecisionService,
    IMediaMaintenanceIntegritySummaryService mediaMaintenanceIntegritySummaryService
) : IMediaMaintenanceIntegrityBatchService
{
    private readonly IMediaMaintenanceIntegrityDecisionService _mediaMaintenanceIntegrityDecisionService =
        mediaMaintenanceIntegrityDecisionService;
    private readonly IMediaMaintenanceIntegritySummaryService _mediaMaintenanceIntegritySummaryService =
        mediaMaintenanceIntegritySummaryService;

    public MediaMaintenanceIntegrityBatchResult Evaluate(
        IReadOnlyList<MediaMaintenanceIntegrityObservation> observations
    )
    {
        List<MediaMaintenanceIntegrityItemResult> items = [];

        foreach (MediaMaintenanceIntegrityObservation observation in observations)
        {
            MediaMaintenanceIntegrityEvaluation evaluation =
                _mediaMaintenanceIntegrityDecisionService.Evaluate(
                    observation.CacheFileSize,
                    observation.IsValidMediaFile
                );

            items.Add(
                new MediaMaintenanceIntegrityItemResult
                {
                    CorrelationId = observation.CorrelationId,
                    Remove = evaluation.Remove,
                    Evaluation = evaluation,
                }
            );
        }

        MediaMaintenanceIntegritySummary summary = _mediaMaintenanceIntegritySummaryService.Summarize(
            items.Select(item => item.Evaluation)
        );

        return new MediaMaintenanceIntegrityBatchResult { Items = items, Summary = summary };
    }
}
