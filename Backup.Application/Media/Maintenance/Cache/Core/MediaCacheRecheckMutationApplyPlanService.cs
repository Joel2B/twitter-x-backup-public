using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckMutationApplyPlanService
    : IMediaCacheRecheckMutationApplyPlanService
{
    public MediaCacheRecheckMutationApplyPlan BuildPlan(
        IReadOnlyList<MediaCacheRecheckMutation> mutations
    )
    {
        List<string> invalidPaths = [];
        List<string> removePaths = [];
        List<MediaCacheEntryState> updatedEntries = [];

        foreach (MediaCacheRecheckMutation mutation in mutations)
        {
            switch (mutation.Kind)
            {
                case MediaCacheRecheckMutationKind.Invalid:
                    invalidPaths.Add(mutation.Path);
                    break;
                case MediaCacheRecheckMutationKind.Remove:
                    removePaths.Add(mutation.Path);
                    break;
                case MediaCacheRecheckMutationKind.Update:
                    if (mutation.UpdatedEntryState is not null)
                        updatedEntries.Add(mutation.UpdatedEntryState);
                    break;
                case MediaCacheRecheckMutationKind.None:
                case MediaCacheRecheckMutationKind.SkipMissing:
                default:
                    break;
            }
        }

        return new MediaCacheRecheckMutationApplyPlan
        {
            InvalidPaths = invalidPaths,
            RemovePaths = removePaths,
            UpdatedEntries = updatedEntries,
        };
    }
}
