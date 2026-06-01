using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenanceIntegrityObservationCompositionService(
    IMediaMaintenanceIntegrityDecisionService mediaMaintenanceIntegrityDecisionService
) : IMediaMaintenanceIntegrityObservationCompositionService
{
    private readonly IMediaMaintenanceIntegrityDecisionService _mediaMaintenanceIntegrityDecisionService =
        mediaMaintenanceIntegrityDecisionService;

    public IReadOnlyList<MediaMaintenanceIntegrityProbeItem> BuildProbeItems(
        IReadOnlyList<MediaMaintenanceIntegrityTarget> targets,
        IReadOnlyDictionary<string, long?> cacheSizesByPath
    )
    {
        List<MediaMaintenanceIntegrityProbeItem> items = [];

        foreach (MediaMaintenanceIntegrityTarget target in targets)
        {
            cacheSizesByPath.TryGetValue(target.Path, out long? size);

            items.Add(
                new MediaMaintenanceIntegrityProbeItem
                {
                    CorrelationId = target.CorrelationId,
                    Path = target.Path,
                    CacheFileSize = size,
                    ShouldProbe = _mediaMaintenanceIntegrityDecisionService.ShouldProbe(size),
                }
            );
        }

        return items;
    }

    public IReadOnlyList<MediaMaintenanceIntegrityObservation> ToObservations(
        IReadOnlyList<MediaMaintenanceIntegrityProbeOutcome> outcomes
    ) =>
        outcomes
            .Select(outcome => new MediaMaintenanceIntegrityObservation
            {
                CorrelationId = outcome.CorrelationId,
                CacheFileSize = outcome.CacheFileSize,
                IsValidMediaFile = outcome.IsValidMediaFile,
            })
            .ToList();
}
