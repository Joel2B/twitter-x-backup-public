using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkEntryStateMutationService
    : IMediaBackupChunkEntryStateMutationService
{
    public IReadOnlyList<MediaBackupChunkEntryState> BuildStates(
        IEnumerable<MediaBackupChunkEntryMutationInput> items
    ) =>
        items
            .Select(item => new MediaBackupChunkEntryState
            {
                Path = item.Path,
                Hash = item.Hash,
                FileSize = item.FileSize,
                Crc32 = item.Crc32,
            })
            .ToList();

    public IReadOnlyList<MediaBackupChunkEntryMutationInput> ApplyStates(
        IEnumerable<MediaBackupChunkEntryMutationInput> items,
        IEnumerable<MediaBackupChunkEntryState> states
    )
    {
        Dictionary<string, MediaBackupChunkEntryState> byPath = states.ToDictionary(
            item => item.Path,
            StringComparer.Ordinal
        );

        return items
            .Select(item =>
            {
                if (!byPath.TryGetValue(item.Path, out MediaBackupChunkEntryState? state))
                    return item;

                return new MediaBackupChunkEntryMutationInput
                {
                    Path = item.Path,
                    Hash = state.Hash,
                    FileSize = state.FileSize,
                    Crc32 = state.Crc32,
                };
            })
            .ToList();
    }
}
