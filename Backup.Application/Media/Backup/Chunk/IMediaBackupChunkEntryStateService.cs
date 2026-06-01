using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkEntryStateService
{
    IReadOnlyList<MediaBackupApplyChunkPathState> BuildApplyChunkPathStates(
        IEnumerable<MediaBackupChunkEntryState> items
    );

    IReadOnlyList<MediaBackupChunkFailureState> BuildFailureStates(
        IEnumerable<MediaBackupChunkEntryState> items
    );

    IReadOnlyList<MediaBackupChunkEntryState> ApplyFailureStates(
        IEnumerable<MediaBackupChunkEntryState> items,
        IReadOnlyDictionary<string, MediaBackupChunkFailureState> byPath
    );

    IReadOnlyList<MediaBackupSyncFinalizeInputChunk> BuildSyncFinalizeInputChunks(
        IEnumerable<MediaBackupChunkPathsState> chunks
    );
}
