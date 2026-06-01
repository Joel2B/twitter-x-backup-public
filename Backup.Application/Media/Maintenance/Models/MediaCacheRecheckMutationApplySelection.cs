namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheRecheckMutationApplySelection
{
    public IReadOnlyList<string> InvalidPaths { get; init; } = [];
    public IReadOnlyList<string> RemoveExistingPaths { get; init; } = [];
    public IReadOnlyList<string> RemoveMissingPaths { get; init; } = [];
    public IReadOnlyList<MediaCacheEntryState> UpdateExistingEntries { get; init; } = [];
    public IReadOnlyList<string> UpdateMissingPaths { get; init; } = [];
}
