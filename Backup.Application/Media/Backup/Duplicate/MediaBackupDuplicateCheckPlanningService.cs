using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDuplicateCheckPlanningService(
    IMediaBackupDuplicateCleanupService duplicateCleanupService,
    IMediaBackupStorageConsistencyDecisionService storageConsistencyDecisionService
) : IMediaBackupDuplicateCheckPlanningService
{
    private readonly IMediaBackupDuplicateCleanupService _duplicateCleanupService =
        duplicateCleanupService;
    private readonly IMediaBackupStorageConsistencyDecisionService _storageConsistencyDecisionService =
        storageConsistencyDecisionService;

    public MediaBackupDuplicateCheckPlan Plan(
        IReadOnlyList<string> memoryPaths,
        IReadOnlyList<string> storagePaths
    )
    {
        IReadOnlyList<MediaPathDuplicateGroup> memoryDuplicates = memoryPaths
            .GroupBy(path => path)
            .Where(group => group.Count() > 1)
            .Select(group => new MediaPathDuplicateGroup
            {
                Path = group.Key,
                Count = group.Count(),
                Entries = group.ToList(),
            })
            .ToList();

        IReadOnlyList<MediaPathDuplicateGroup> storageDuplicates = storagePaths
            .GroupBy(path => path)
            .Where(group => group.Count() > 1)
            .Select(group => new MediaPathDuplicateGroup
            {
                Path = group.Key,
                Count = group.Count(),
                Entries = group.ToList(),
            })
            .ToList();

        MediaBackupDuplicateCleanupPlan? cleanupPlan =
            storageDuplicates.Count == 0
                ? null
                : _duplicateCleanupService.BuildPlan(storageDuplicates);

        MediaBackupStorageConsistencyDecision consistencyDecision =
            _storageConsistencyDecisionService.DecideForDuplicateCheck(memoryPaths, storagePaths);

        return new MediaBackupDuplicateCheckPlan
        {
            MemoryDuplicatePathCount = memoryDuplicates.Count,
            MemoryDuplicateEntryCount = memoryDuplicates.Sum(item => item.Entries.Count),
            StorageDuplicatePathCount = storageDuplicates.Count,
            StorageDuplicateEntryCount = storageDuplicates.Sum(item => item.Entries.Count),
            StorageCleanupPlan = cleanupPlan,
            ConsistencyDecision = consistencyDecision,
        };
    }
}
