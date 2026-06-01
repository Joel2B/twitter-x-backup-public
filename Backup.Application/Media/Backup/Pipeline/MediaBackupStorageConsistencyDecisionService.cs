using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupStorageConsistencyDecisionService(
    IMediaBackupChunkReconciliationService chunkReconciliationService
) : IMediaBackupStorageConsistencyDecisionService
{
    private readonly IMediaBackupChunkReconciliationService _chunkReconciliationService =
        chunkReconciliationService;

    public MediaBackupStorageConsistencyDecision DecideForApply(
        IEnumerable<string> expectedPaths,
        IEnumerable<string> actualPaths
    )
    {
        MediaBackupChunkReconciliationResult reconciliation = _chunkReconciliationService.Reconcile(
            expectedPaths,
            actualPaths
        );

        return new MediaBackupStorageConsistencyDecision
        {
            MissingCount = reconciliation.MissingCount,
            ExtraPaths = reconciliation.ExtraPaths,
            IsConsistent = reconciliation.IsConsistent,
            ShouldFail = reconciliation.ShouldFail,
            ShouldRemoveExtras = !reconciliation.ShouldFail && reconciliation.ExtraPaths.Count > 0,
        };
    }

    public MediaBackupStorageConsistencyDecision DecideForDuplicateCheck(
        IEnumerable<string> expectedPaths,
        IEnumerable<string> actualPaths
    )
    {
        MediaBackupChunkReconciliationResult reconciliation = _chunkReconciliationService.Reconcile(
            expectedPaths,
            actualPaths
        );

        return new MediaBackupStorageConsistencyDecision
        {
            MissingCount = reconciliation.MissingCount,
            ExtraPaths = reconciliation.ExtraPaths,
            IsConsistent = reconciliation.IsConsistent,
            ShouldFail = false,
            ShouldRemoveExtras = reconciliation.ExtraPaths.Count > 0,
        };
    }
}
