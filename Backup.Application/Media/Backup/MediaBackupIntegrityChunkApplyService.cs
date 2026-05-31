using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupIntegrityChunkApplyService : IMediaBackupIntegrityChunkApplyService
{
    public MediaBackupIntegrityChunkApplyResult Apply(
        IEnumerable<MediaBackupChunkEntryState> entries,
        MediaBackupIntegrityUpdateSelectionPlan selection
    )
    {
        HashSet<string> selectedPaths = [.. selection.SelectedPaths];
        List<string> updatedPaths = [];

        IReadOnlyList<MediaBackupChunkEntryState> updatedEntries = entries
            .Select(entry =>
            {
                if (!selectedPaths.Contains(entry.Path))
                    return entry;

                if (!selection.PathMetadata.TryGetValue(entry.Path, out MediaBackupChunkDataMetadata? metadata))
                    return entry;

                updatedPaths.Add(entry.Path);

                return new MediaBackupChunkEntryState
                {
                    Path = entry.Path,
                    Hash = entry.Hash,
                    FileSize = metadata.FileSize,
                    Crc32 = metadata.Crc32,
                };
            })
            .ToList();

        return new MediaBackupIntegrityChunkApplyResult
        {
            Entries = updatedEntries,
            UpdatedPaths = updatedPaths,
        };
    }
}
