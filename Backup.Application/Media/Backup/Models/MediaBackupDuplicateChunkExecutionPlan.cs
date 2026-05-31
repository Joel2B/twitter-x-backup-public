namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupDuplicateChunkExecutionPlan
{
    public required bool HasMemoryDuplicates { get; init; }
    public required int MemoryDuplicatePathCount { get; init; }
    public required int MemoryDuplicateEntryCount { get; init; }
    public required bool HasStorageDuplicates { get; init; }
    public required int StorageDuplicatePathCount { get; init; }
    public required int StorageDuplicateEntryCount { get; init; }
    public required int RemovedDuplicatePathCount { get; init; }
    public required IReadOnlyList<MediaBackupDuplicateCleanupOperation> CleanupOperations { get; init; }
    public required bool IsConsistent { get; init; }
    public required int MissingCount { get; init; }
    public required int ExtrasCount { get; init; }
    public required bool ShouldRemoveExtras { get; init; }
    public required IReadOnlyList<string> ExtraPathsToRemove { get; init; }
    public required IReadOnlyList<string> ExtraPathsPreview { get; init; }
}
