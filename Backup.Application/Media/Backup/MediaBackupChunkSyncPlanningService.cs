using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkSyncPlanningService : IMediaBackupChunkSyncPlanningService
{
    public MediaBackupChunkSyncPlan Plan(
        IReadOnlyList<MediaBackupChunkPathsState> chunks,
        IEnumerable<string> pathsInBoth
    )
    {
        HashSet<string> intersection = [.. pathsInBoth];
        List<MediaBackupChunkSyncChunkPlan> chunkPlans = [];
        List<string> directPathsToAdd = [];

        foreach (MediaBackupChunkPathsState chunk in chunks)
        {
            List<string> pathsToRemove = chunk
                .Paths.Where(path => intersection.Contains(path))
                .ToList();

            if (pathsToRemove.Count == 0)
                continue;

            chunkPlans.Add(
                new MediaBackupChunkSyncChunkPlan
                {
                    ChunkId = chunk.Id,
                    PathsToRemove = pathsToRemove,
                }
            );

            directPathsToAdd.AddRange(pathsToRemove);
        }

        return new MediaBackupChunkSyncPlan
        {
            Chunks = chunkPlans,
            DirectPathsToAdd = directPathsToAdd,
        };
    }
}
