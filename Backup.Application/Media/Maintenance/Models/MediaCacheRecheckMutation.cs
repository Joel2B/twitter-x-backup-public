namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheRecheckMutation
{
    public required string Path { get; init; }
    public MediaCacheRecheckMutationKind Kind { get; init; }
    public MediaCacheEntryState? UpdatedEntryState { get; init; }
}
