using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkReconciliationService
{
    MediaBackupChunkReconciliationResult Reconcile(
        IEnumerable<string> expectedPaths,
        IEnumerable<string> actualPaths
    );
}
