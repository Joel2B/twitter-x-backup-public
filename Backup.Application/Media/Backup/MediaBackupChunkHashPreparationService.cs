using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkHashPreparationService : IMediaBackupChunkHashPreparationService
{
    public IReadOnlyList<string> SelectPathsNeedingHash(
        IEnumerable<MediaBackupChunkEntryState> entries
    ) => entries.Where(entry => entry.Hash is null).Select(entry => entry.Path).ToList();

    public IReadOnlyList<MediaBackupChunkEntryState> ApplyHashes(
        IEnumerable<MediaBackupChunkEntryState> entries,
        IReadOnlyDictionary<string, string?> hashByPath
    ) =>
        entries
            .Select(entry =>
            {
                if (!hashByPath.TryGetValue(entry.Path, out string? hash))
                    return entry;

                return new MediaBackupChunkEntryState
                {
                    Path = entry.Path,
                    Hash = hash,
                    FileSize = entry.FileSize,
                    Crc32 = entry.Crc32,
                };
            })
            .ToList();
}
