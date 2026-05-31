using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkCountDeltaService : IMediaBackupChunkCountDeltaService
{
    public MediaBackupChunkCountDeltaResult Compare(
        IEnumerable<MediaBackupChunkCountState> before,
        IEnumerable<MediaBackupChunkCountState> after
    )
    {
        Dictionary<int, int> beforeByChunk = before.ToDictionary(item => item.ChunkId, item => item.PathCount);
        Dictionary<int, int> afterByChunk = after.ToDictionary(item => item.ChunkId, item => item.PathCount);

        HashSet<int> chunkIds = [.. beforeByChunk.Keys, .. afterByChunk.Keys];
        List<MediaBackupChunkCountDeltaItem> items = [];
        int totalAddedPaths = 0;

        foreach (int chunkId in chunkIds.Order())
        {
            int beforeCount = beforeByChunk.GetValueOrDefault(chunkId, 0);
            int afterCount = afterByChunk.GetValueOrDefault(chunkId, 0);
            int difference = afterCount - beforeCount;

            totalAddedPaths += difference;

            items.Add(
                new MediaBackupChunkCountDeltaItem
                {
                    ChunkId = chunkId,
                    BeforeCount = beforeCount,
                    AfterCount = afterCount,
                    Difference = difference,
                }
            );
        }

        return new MediaBackupChunkCountDeltaResult
        {
            TotalAddedPaths = totalAddedPaths,
            Items = items,
        };
    }
}
