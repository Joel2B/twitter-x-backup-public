namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheRecheckEvaluation
{
    public required string Path { get; init; }
    public bool IsInvalid { get; init; }
    public bool ShouldRemove { get; init; }
    public MediaCacheEntryState? UpdatedEntryState { get; init; }
}
