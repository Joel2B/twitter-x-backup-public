using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheStoredEntryProjectionService : IMediaCacheStoredEntryProjectionService
{
    public IReadOnlyList<MediaCacheRecheckCandidate> ToRecheckCandidates(
        IEnumerable<MediaCacheStoredEntry> entries
    )
    {
        List<MediaCacheRecheckCandidate> candidates = [];

        foreach (MediaCacheStoredEntry entry in entries)
        {
            candidates.Add(
                new MediaCacheRecheckCandidate
                {
                    Path = entry.Path,
                    StreamSizeBytes = entry.StreamSizeBytes,
                    FileSizeBytes = entry.FileSizeBytes,
                }
            );
        }

        return candidates;
    }

    public IEnumerable<KeyValuePair<int?, long?>> ToPartitionFileSizes(
        IEnumerable<MediaCacheStoredEntry> entries
    )
    {
        foreach (MediaCacheStoredEntry entry in entries)
            yield return new KeyValuePair<int?, long?>(entry.PartitionId, entry.FileSizeBytes);
    }
}
