namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheLoadExecutionResult
{
    public IReadOnlyCollection<string> RecheckPaths { get; init; } = [];
    public IReadOnlyList<MediaCacheRecheckMutation> Mutations { get; init; } = [];
    public IReadOnlyList<string> FailedProbePaths { get; init; } = [];
}
