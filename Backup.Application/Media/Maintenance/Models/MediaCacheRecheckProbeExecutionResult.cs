namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheRecheckProbeExecutionResult
{
    public required IReadOnlyList<MediaCacheRecheckObservation> Observations { get; init; }

    public required IReadOnlyList<string> FailedPaths { get; init; }
}
