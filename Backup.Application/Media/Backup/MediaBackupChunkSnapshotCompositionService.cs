using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkSnapshotCompositionService
    : IMediaBackupChunkSnapshotCompositionService
{
    public IReadOnlyList<MediaBackupChunkCountState> BuildChunkCountStates(
        IEnumerable<MediaBackupChunkPathsState> chunks
    ) =>
        chunks
            .Select(chunk => new MediaBackupChunkCountState
            {
                ChunkId = chunk.Id,
                PathCount = chunk.Paths.Count,
            })
            .ToList();

    public MediaBackupChunkPathMaps BuildPathMaps(
        IEnumerable<MediaBackupChunkPathsState> before,
        IEnumerable<MediaBackupChunkPathsState> after
    )
    {
        Dictionary<int, IReadOnlyList<string>> beforePathsByChunk = before.ToDictionary(
            item => item.Id,
            item => item.Paths
        );
        Dictionary<int, IReadOnlyList<string>> afterPathsByChunk = after.ToDictionary(
            item => item.Id,
            item => item.Paths
        );

        HashSet<string> distinctPaths = [];

        foreach (IReadOnlyList<string> paths in beforePathsByChunk.Values)
            distinctPaths.UnionWith(paths);

        foreach (IReadOnlyList<string> paths in afterPathsByChunk.Values)
            distinctPaths.UnionWith(paths);

        return new MediaBackupChunkPathMaps
        {
            BeforePathsByChunk = beforePathsByChunk,
            AfterPathsByChunk = afterPathsByChunk,
            DistinctPathsForSizeLookup = distinctPaths.ToList(),
        };
    }
}
