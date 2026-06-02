using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupChunkStateRuntimeAdapter(
    IMediaBackupChunkEntryStateOrchestrationService chunkEntryStateOrchestrationService
)
{
    private readonly IMediaBackupChunkEntryStateOrchestrationService _chunkEntryStateOrchestrationService =
        chunkEntryStateOrchestrationService;

    public IReadOnlyList<MediaBackupChunkEntryState> BuildStates(IEnumerable<ChunkData> items) =>
        _chunkEntryStateOrchestrationService.BuildStates(items.Select(ToEntryRecord));

    public void ApplyStates(Chunk chunk, IEnumerable<MediaBackupChunkEntryState> states)
    {
        IReadOnlyList<MediaBackupChunkEntryRecord> updated =
            _chunkEntryStateOrchestrationService.ApplyStates(
                chunk.Data.Select(ToEntryRecord),
                states
            );

        Dictionary<string, MediaBackupChunkEntryRecord> byPath = updated.ToDictionary(
            item => item.Path,
            StringComparer.Ordinal
        );

        foreach (ChunkData data in chunk.Data)
        {
            if (!byPath.TryGetValue(data.Path, out MediaBackupChunkEntryRecord? state))
                continue;

            data.Hash = state.Hash;
            data.FileSize = state.FileSize;
            data.Crc32 = state.Crc32;
        }
    }

    private static MediaBackupChunkEntryRecord ToEntryRecord(ChunkData item) =>
        new()
        {
            Path = item.Path,
            Hash = item.Hash,
            FileSize = item.FileSize,
            Crc32 = item.Crc32,
        };
}
