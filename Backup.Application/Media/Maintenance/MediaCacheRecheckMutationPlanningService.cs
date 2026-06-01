using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheRecheckMutationPlanningService
    : IMediaCacheRecheckMutationPlanningService
{
    public IReadOnlyList<MediaCacheRecheckMutation> Plan(
        IReadOnlyCollection<MediaCacheRecheckEvaluation> evaluations,
        IReadOnlySet<string> existingPaths
    )
    {
        List<MediaCacheRecheckMutation> mutations = [];

        foreach (MediaCacheRecheckEvaluation evaluation in evaluations)
        {
            if (evaluation.IsInvalid)
            {
                mutations.Add(
                    new MediaCacheRecheckMutation
                    {
                        Path = evaluation.Path,
                        Kind = MediaCacheRecheckMutationKind.Invalid,
                    }
                );
                continue;
            }

            bool exists = existingPaths.Contains(evaluation.Path);

            if (evaluation.ShouldRemove)
            {
                mutations.Add(
                    new MediaCacheRecheckMutation
                    {
                        Path = evaluation.Path,
                        Kind = exists
                            ? MediaCacheRecheckMutationKind.Remove
                            : MediaCacheRecheckMutationKind.SkipMissing,
                    }
                );
                continue;
            }

            if (evaluation.UpdatedEntryState is null)
            {
                mutations.Add(
                    new MediaCacheRecheckMutation
                    {
                        Path = evaluation.Path,
                        Kind = MediaCacheRecheckMutationKind.None,
                    }
                );
                continue;
            }

            mutations.Add(
                new MediaCacheRecheckMutation
                {
                    Path = evaluation.Path,
                    Kind = exists
                        ? MediaCacheRecheckMutationKind.Update
                        : MediaCacheRecheckMutationKind.SkipMissing,
                    UpdatedEntryState = evaluation.UpdatedEntryState,
                }
            );
        }

        return mutations;
    }
}
