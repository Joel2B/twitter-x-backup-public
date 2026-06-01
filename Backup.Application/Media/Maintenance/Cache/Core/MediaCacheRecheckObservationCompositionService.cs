using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckObservationCompositionService
    : IMediaCacheRecheckObservationCompositionService
{
    public IReadOnlyList<MediaCacheRecheckProbeInput> BuildProbeInputs(
        IReadOnlyCollection<string> pathsToRecheck,
        IReadOnlyCollection<MediaCacheStoredEntry> entries
    )
    {
        HashSet<string> requested = pathsToRecheck.ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<MediaCacheRecheckProbeInput> inputs = [];

        foreach (MediaCacheStoredEntry entry in entries)
        {
            if (!requested.Contains(entry.Path))
                continue;

            inputs.Add(
                new MediaCacheRecheckProbeInput
                {
                    Path = entry.Path,
                    PartitionId = entry.PartitionId,
                    StreamSizeBytes = entry.StreamSizeBytes,
                }
            );
        }

        return inputs;
    }

    public IReadOnlyList<MediaCacheRecheckObservation> ToObservations(
        IReadOnlyList<MediaCacheRecheckProbeOutcome> outcomes
    ) =>
        outcomes
            .Select(outcome => new MediaCacheRecheckObservation
            {
                Path = outcome.Path,
                PartitionId = outcome.PartitionId,
                StreamSizeBytes = outcome.StreamSizeBytes,
                FileExists = outcome.FileExists,
                FileSizeBytes = outcome.FileSizeBytes,
            })
            .ToList();
}
