using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckOrchestrationService
{
    IReadOnlyCollection<string> SelectRecheckPaths(
        IReadOnlyCollection<MediaCacheRecheckCandidate> candidates
    );
    MediaCacheRecheckResult Evaluate(MediaCacheRecheckObservation observation);
}
