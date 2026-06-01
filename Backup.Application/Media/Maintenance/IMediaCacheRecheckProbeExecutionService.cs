using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckProbeExecutionService
{
    MediaCacheRecheckProbeExecutionResult Execute(
        IReadOnlyList<MediaCacheRecheckProbeInput> probeInputs,
        Func<MediaCacheRecheckProbeInput, MediaCacheRecheckProbeOutcome> probe
    );
}
