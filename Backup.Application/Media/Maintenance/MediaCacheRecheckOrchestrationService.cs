using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckOrchestrationService(
    IMediaCacheRecheckPolicyService mediaCacheRecheckPolicyService
) : IMediaCacheRecheckOrchestrationService
{
    private readonly IMediaCacheRecheckPolicyService _mediaCacheRecheckPolicyService =
        mediaCacheRecheckPolicyService;

    public IReadOnlyCollection<string> SelectRecheckPaths(
        IReadOnlyCollection<MediaCacheRecheckCandidate> candidates
    )
    {
        HashSet<string> recheck = new(StringComparer.OrdinalIgnoreCase);

        foreach (MediaCacheRecheckCandidate candidate in candidates)
        {
            if (
                !_mediaCacheRecheckPolicyService.ShouldRecheck(
                    candidate.StreamSizeBytes,
                    candidate.FileSizeBytes
                )
            )
                continue;

            recheck.Add(candidate.Path);
        }

        return recheck.ToList();
    }

    public MediaCacheRecheckResult Evaluate(MediaCacheRecheckObservation observation)
    {
        if (observation.PartitionId is null || observation.StreamSizeBytes is null)
            return new MediaCacheRecheckResult { IsInvalid = true };

        if (!observation.FileExists)
            return new MediaCacheRecheckResult { ShouldRemove = true };

        return new MediaCacheRecheckResult
        {
            ShouldUpdate = true,
            PartitionId = observation.PartitionId,
            StreamSizeBytes = observation.StreamSizeBytes,
            FileSizeBytes = observation.FileSizeBytes,
        };
    }
}
