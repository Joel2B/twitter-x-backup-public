namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupDuplicateCheckPlan
{
    public int MemoryDuplicatePathCount { get; init; }
    public int MemoryDuplicateEntryCount { get; init; }
    public int StorageDuplicatePathCount { get; init; }
    public int StorageDuplicateEntryCount { get; init; }
    public MediaBackupDuplicateCleanupPlan? StorageCleanupPlan { get; init; }
    public required MediaBackupStorageConsistencyDecision ConsistencyDecision { get; init; }
}
