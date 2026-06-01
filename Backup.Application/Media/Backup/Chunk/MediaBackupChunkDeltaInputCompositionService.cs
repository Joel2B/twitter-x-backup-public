using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkDeltaInputCompositionService
    : IMediaBackupChunkDeltaInputCompositionService
{
    public IReadOnlyList<MediaBackupChunkDeltaLogInput> Compose(
        IEnumerable<MediaBackupChunkCountDeltaItem> deltas,
        IReadOnlyDictionary<int, IReadOnlyList<string>> beforePathsByChunk,
        IReadOnlyDictionary<int, IReadOnlyList<string>> afterPathsByChunk,
        IReadOnlyDictionary<string, long> sizeByPath
    )
    {
        List<MediaBackupChunkDeltaLogInput> result = [];

        foreach (MediaBackupChunkCountDeltaItem delta in deltas)
        {
            beforePathsByChunk.TryGetValue(delta.ChunkId, out IReadOnlyList<string>? beforePaths);
            afterPathsByChunk.TryGetValue(delta.ChunkId, out IReadOnlyList<string>? afterPaths);

            long sizeBefore = SumSize(beforePaths ?? [], sizeByPath);
            long sizeAfter = SumSize(afterPaths ?? [], sizeByPath);

            result.Add(
                new MediaBackupChunkDeltaLogInput
                {
                    ChunkId = delta.ChunkId,
                    BeforeCount = delta.BeforeCount,
                    AfterCount = delta.AfterCount,
                    Difference = delta.Difference,
                    SizeBeforeBytes = sizeBefore,
                    SizeAfterBytes = sizeAfter,
                }
            );
        }

        return result;
    }

    private static long SumSize(
        IReadOnlyList<string> paths,
        IReadOnlyDictionary<string, long> sizeByPath
    )
    {
        long total = 0;

        foreach (string path in paths)
        {
            if (sizeByPath.TryGetValue(path, out long value))
                total += value;
        }

        return total;
    }
}
