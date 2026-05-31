using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupPathCandidateCompositionService
    : IMediaBackupPathCandidateCompositionService
{
    public IReadOnlyList<MediaBackupPathCandidate> Compose(
        IEnumerable<MediaBackupPathCacheObservation> observations,
        ISet<string> assignedCachePaths
    )
    {
        List<MediaBackupPathCandidate> candidates = [];

        foreach (MediaBackupPathCacheObservation item in observations)
        {
            if (!item.CacheExists)
                continue;

            candidates.Add(
                new MediaBackupPathCandidate
                {
                    OriginalPath = item.OriginalPath,
                    CachePath = item.CachePath,
                    FileSizeBytes = item.FileSizeBytes,
                    IsAlreadyAssigned = assignedCachePaths.Contains(item.CachePath),
                }
            );
        }

        return candidates;
    }
}
