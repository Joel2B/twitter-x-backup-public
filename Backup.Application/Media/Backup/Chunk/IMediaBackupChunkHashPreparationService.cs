using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkHashPreparationService
{
    IReadOnlyList<string> SelectPathsNeedingHash(IEnumerable<MediaBackupChunkEntryState> entries);

    IReadOnlyList<MediaBackupChunkEntryState> ApplyHashes(
        IEnumerable<MediaBackupChunkEntryState> entries,
        IReadOnlyDictionary<string, string?> hashByPath
    );
}
