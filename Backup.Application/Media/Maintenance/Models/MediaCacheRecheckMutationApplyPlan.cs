namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheRecheckMutationApplyPlan
{
    public IReadOnlyList<string> InvalidPaths { get; init; } = [];
    public IReadOnlyList<string> RemovePaths { get; init; } = [];
    public IReadOnlyList<MediaCacheEntryState> UpdatedEntries { get; init; } = [];
}
