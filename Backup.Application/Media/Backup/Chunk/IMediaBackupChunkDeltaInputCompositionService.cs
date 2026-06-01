using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkDeltaInputCompositionService
{
    IReadOnlyList<MediaBackupChunkDeltaLogInput> Compose(
        IEnumerable<MediaBackupChunkCountDeltaItem> deltas,
        IReadOnlyDictionary<int, IReadOnlyList<string>> beforePathsByChunk,
        IReadOnlyDictionary<int, IReadOnlyList<string>> afterPathsByChunk,
        IReadOnlyDictionary<string, long> sizeByPath
    );
}
