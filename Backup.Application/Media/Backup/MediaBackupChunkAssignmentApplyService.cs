using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkAssignmentApplyService : IMediaBackupChunkAssignmentApplyService
{
    public MediaBackupChunkAssignmentApplyResult Apply(IEnumerable<MediaBackupPathAssignment> assignments)
    {
        Dictionary<int, List<string>> addedByChunk = [];
        List<string> addedOriginalPaths = [];

        foreach (MediaBackupPathAssignment assignment in assignments)
        {
            if (!addedByChunk.TryGetValue(assignment.ChunkId, out List<string>? chunkPaths))
            {
                chunkPaths = [];
                addedByChunk[assignment.ChunkId] = chunkPaths;
            }

            chunkPaths.Add(assignment.CachePath);
            addedOriginalPaths.Add(assignment.OriginalPath);
        }

        return new MediaBackupChunkAssignmentApplyResult
        {
            AddedCachePathsByChunk = addedByChunk.ToDictionary(
                item => item.Key,
                item => (IReadOnlyList<string>)item.Value
            ),
            AddedOriginalPaths = addedOriginalPaths,
        };
    }
}
