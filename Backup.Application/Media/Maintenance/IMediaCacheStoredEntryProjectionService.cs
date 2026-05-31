using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheStoredEntryProjectionService
{
    IReadOnlyList<MediaCacheRecheckCandidate> ToRecheckCandidates(
        IEnumerable<MediaCacheStoredEntry> entries
    );

    IEnumerable<KeyValuePair<int?, long?>> ToPartitionFileSizes(
        IEnumerable<MediaCacheStoredEntry> entries
    );
}
