using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupIntegrityChunkDataSelectionService
    : IMediaBackupIntegrityChunkDataSelectionService
{
    public MediaBackupIntegrityChunkDataSelectionResult Select(
        IEnumerable<string> changedPaths,
        IEnumerable<string> chunkPaths
    )
    {
        HashSet<string> changed = [.. changedPaths];
        HashSet<string> chunk = [.. chunkPaths];

        List<string> selected = chunk.Where(changed.Contains).ToList();
        List<string> missing = changed.Where(path => !chunk.Contains(path)).ToList();

        return new MediaBackupIntegrityChunkDataSelectionResult
        {
            SelectedPaths = selected,
            MissingPaths = missing,
        };
    }
}
