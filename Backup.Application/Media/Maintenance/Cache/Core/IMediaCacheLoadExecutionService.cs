using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheLoadExecutionService
{
    MediaCacheLoadExecutionResult Execute(
        IReadOnlyList<MediaCacheStoredEntry> entries,
        IReadOnlyCollection<string> existingCachePaths,
        Func<
            IReadOnlyList<MediaCacheRecheckProbeInput>,
            MediaCacheRecheckProbeExecutionResult
        > probe
    );
}
