using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkEntryStateService : IMediaBackupChunkEntryStateService
{
    public IReadOnlyList<MediaBackupApplyChunkPathState> BuildApplyChunkPathStates(
        IEnumerable<MediaBackupChunkEntryState> items
    ) =>
        items
            .Select(item => new MediaBackupApplyChunkPathState
            {
                SourcePath = item.Path,
                HasHash = item.Hash is not null,
            })
            .ToList();

    public IReadOnlyList<MediaBackupChunkFailureState> BuildFailureStates(
        IEnumerable<MediaBackupChunkEntryState> items
    ) =>
        items
            .Select(item => new MediaBackupChunkFailureState
            {
                Path = item.Path,
                Hash = item.Hash,
                FileSize = item.FileSize,
                Crc32 = item.Crc32,
            })
            .ToList();

    public IReadOnlyList<MediaBackupChunkEntryState> ApplyFailureStates(
        IEnumerable<MediaBackupChunkEntryState> items,
        IReadOnlyDictionary<string, MediaBackupChunkFailureState> byPath
    ) =>
        items
            .Select(item =>
            {
                if (!byPath.TryGetValue(item.Path, out MediaBackupChunkFailureState? state))
                    return item;

                return new MediaBackupChunkEntryState
                {
                    Path = item.Path,
                    Hash = state.Hash,
                    FileSize = state.FileSize,
                    Crc32 = state.Crc32,
                };
            })
            .ToList();

    public IReadOnlyList<MediaBackupSyncFinalizeInputChunk> BuildSyncFinalizeInputChunks(
        IEnumerable<MediaBackupChunkPathsState> chunks
    ) =>
        chunks
            .Select(chunk => new MediaBackupSyncFinalizeInputChunk
            {
                ChunkId = chunk.Id,
                Paths = chunk.Paths,
            })
            .ToList();
}
