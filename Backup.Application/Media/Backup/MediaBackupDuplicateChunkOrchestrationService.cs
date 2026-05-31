using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDuplicateChunkOrchestrationService
    : IMediaBackupDuplicateChunkOrchestrationService
{
    public MediaBackupDuplicateChunkExecutionPlan BuildExecutionPlan(
        MediaBackupDuplicateCheckPlan plan,
        int extraPreviewLimit
    )
    {
        MediaBackupDuplicateCleanupPlan? cleanupPlan = plan.StorageCleanupPlan;
        MediaBackupStorageConsistencyDecision decision = plan.ConsistencyDecision;
        IReadOnlyList<string> extras = decision.ExtraPaths.ToList();

        return new MediaBackupDuplicateChunkExecutionPlan
        {
            HasMemoryDuplicates = plan.MemoryDuplicatePathCount != 0,
            MemoryDuplicatePathCount = plan.MemoryDuplicatePathCount,
            MemoryDuplicateEntryCount = plan.MemoryDuplicateEntryCount,
            HasStorageDuplicates = plan.StorageDuplicatePathCount != 0,
            StorageDuplicatePathCount = plan.StorageDuplicatePathCount,
            StorageDuplicateEntryCount = plan.StorageDuplicateEntryCount,
            RemovedDuplicatePathCount = cleanupPlan?.RemovedPathCount ?? 0,
            CleanupOperations = cleanupPlan?.Operations.ToList() ?? [],
            IsConsistent = decision.IsConsistent,
            MissingCount = decision.MissingCount,
            ExtrasCount = extras.Count,
            ShouldRemoveExtras = decision.ShouldRemoveExtras,
            ExtraPathsToRemove = extras,
            ExtraPathsPreview = extras.Take(extraPreviewLimit).ToList(),
        };
    }

    public int UpdateStorageCount(int currentStorageCount, int storageEntriesRead, int removedExtrasCount)
        => currentStorageCount + storageEntriesRead - removedExtrasCount;
}
