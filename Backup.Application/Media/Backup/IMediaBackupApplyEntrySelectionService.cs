using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupApplyEntrySelectionService
{
    IReadOnlyList<MediaBackupApplyEntryCandidate> SelectEntriesToAdd(
        IEnumerable<MediaBackupApplyEntryCandidate> candidates,
        ISet<string> storagePaths
    );
}
