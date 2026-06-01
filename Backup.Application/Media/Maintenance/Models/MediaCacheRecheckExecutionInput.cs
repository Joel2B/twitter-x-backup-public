namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheRecheckExecutionInput
{
    public IReadOnlyCollection<string> RecheckPaths { get; init; } = [];
    public IReadOnlyList<MediaCacheRecheckProbeInput> ProbeInputs { get; init; } = [];
}
