using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupApplyEntrySelectionService : IMediaBackupApplyEntrySelectionService
{
    public IReadOnlyList<MediaBackupApplyEntryCandidate> SelectEntriesToAdd(
        IEnumerable<MediaBackupApplyEntryCandidate> candidates,
        ISet<string> storagePaths
    ) =>
        candidates
            .Where(item => item.HasHash && !storagePaths.Contains(item.ArchivePath))
            .ToList();
}
