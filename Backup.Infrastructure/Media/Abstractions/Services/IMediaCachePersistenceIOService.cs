using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaCachePersistenceIOService
{
    Task<IReadOnlyList<MediaCacheEntry>> LoadIncrementalSnapshots(
        string directory,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<MediaCacheEntry>> LoadPrimarySnapshot(
        string file,
        CancellationToken cancellationToken = default
    );

    Task SavePrimarySnapshot(
        string file,
        IReadOnlyCollection<MediaCacheEntry> entries,
        CancellationToken cancellationToken = default
    );

    Task SaveIncrementalSnapshot(
        string directory,
        MediaCacheEntry entry,
        string fileName,
        CancellationToken cancellationToken = default
    );

    Task ReplicatePrimarySnapshot(
        string primaryFilePath,
        IReadOnlyCollection<string> replicaPaths,
        CancellationToken cancellationToken = default
    );

    void ResetIncrementalSnapshotDirectory(string directory);
}
