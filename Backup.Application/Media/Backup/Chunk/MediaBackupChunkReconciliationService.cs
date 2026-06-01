using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkReconciliationService : IMediaBackupChunkReconciliationService
{
    public MediaBackupChunkReconciliationResult Reconcile(
        IEnumerable<string> expectedPaths,
        IEnumerable<string> actualPaths
    )
    {
        List<string> expected = expectedPaths.ToList();
        List<string> actual = actualPaths.ToList();

        return new MediaBackupChunkReconciliationResult
        {
            MissingCount = expected.Except(actual).Count(),
            ExtraPaths = actual.Except(expected).ToList(),
        };
    }
}
