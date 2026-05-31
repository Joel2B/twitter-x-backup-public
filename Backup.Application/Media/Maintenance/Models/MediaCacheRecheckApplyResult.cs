namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheRecheckApplyResult
{
    public bool IsInvalid { get; init; }
    public bool ShouldRemove { get; init; }
    public MediaCacheEntryState? UpdatedEntryState { get; init; }
}
