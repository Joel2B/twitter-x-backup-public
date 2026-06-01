using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckObservationCompositionService
{
    IReadOnlyList<MediaCacheRecheckProbeInput> BuildProbeInputs(
        IReadOnlyCollection<string> pathsToRecheck,
        IReadOnlyCollection<MediaCacheStoredEntry> entries
    );

    IReadOnlyList<MediaCacheRecheckObservation> ToObservations(
        IReadOnlyList<MediaCacheRecheckProbeOutcome> outcomes
    );
}
