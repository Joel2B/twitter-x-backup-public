using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkLoadExecutionService
{
    Task<IReadOnlyList<TChunk>?> ExecuteAsync<TChunk>(
        string? chunkDataFileName,
        IReadOnlyCollection<int>? chunkIds,
        CancellationToken token,
        Func<MediaBackupChunkReadDescriptor, CancellationToken, Task<TChunk>> readChunk,
        Action<int>? onChunkProcessed = null
    );
}
