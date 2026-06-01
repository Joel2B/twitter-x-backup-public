using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupIntegrityChunkApplyService
{
    MediaBackupIntegrityChunkApplyResult Apply(
        IEnumerable<MediaBackupChunkEntryState> entries,
        MediaBackupIntegrityUpdateSelectionPlan selection
    );
}
