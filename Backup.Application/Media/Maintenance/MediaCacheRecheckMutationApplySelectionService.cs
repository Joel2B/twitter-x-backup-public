using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckMutationApplySelectionService
    : IMediaCacheRecheckMutationApplySelectionService
{
    public MediaCacheRecheckMutationApplySelection Select(
        MediaCacheRecheckMutationApplyPlan plan,
        IReadOnlySet<string> existingPaths
    )
    {
        List<string> removeExistingPaths = [];
        List<string> removeMissingPaths = [];
        List<MediaCacheEntryState> updateExistingEntries = [];
        List<string> updateMissingPaths = [];

        foreach (string path in plan.RemovePaths)
        {
            if (existingPaths.Contains(path))
                removeExistingPaths.Add(path);
            else
                removeMissingPaths.Add(path);
        }

        foreach (MediaCacheEntryState entry in plan.UpdatedEntries)
        {
            if (existingPaths.Contains(entry.Path))
                updateExistingEntries.Add(entry);
            else
                updateMissingPaths.Add(entry.Path);
        }

        return new MediaCacheRecheckMutationApplySelection
        {
            InvalidPaths = plan.InvalidPaths,
            RemoveExistingPaths = removeExistingPaths,
            RemoveMissingPaths = removeMissingPaths,
            UpdateExistingEntries = updateExistingEntries,
            UpdateMissingPaths = updateMissingPaths,
        };
    }
}
