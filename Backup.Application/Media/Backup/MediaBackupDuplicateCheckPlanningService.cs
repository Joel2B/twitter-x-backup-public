using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDuplicateCheckPlanningService(
    IMediaBackupPathAnalysisService pathAnalysisService,
    IMediaBackupDuplicateCleanupService duplicateCleanupService,
    IMediaBackupStorageConsistencyDecisionService storageConsistencyDecisionService
) : IMediaBackupDuplicateCheckPlanningService
{
    private readonly IMediaBackupPathAnalysisService _pathAnalysisService = pathAnalysisService;
    private readonly IMediaBackupDuplicateCleanupService _duplicateCleanupService =
        duplicateCleanupService;
    private readonly IMediaBackupStorageConsistencyDecisionService _storageConsistencyDecisionService =
        storageConsistencyDecisionService;

    public MediaBackupDuplicateCheckPlan Plan(
        IReadOnlyList<string> memoryPaths,
        IReadOnlyList<string> storagePaths
    )
    {
        IReadOnlyList<MediaPathDuplicateGroup> memoryDuplicates = _pathAnalysisService.FindDuplicates(
            memoryPaths
        );
        IReadOnlyList<MediaPathDuplicateGroup> storageDuplicates = _pathAnalysisService.FindDuplicates(
            storagePaths
        );

        MediaBackupDuplicateCleanupPlan? cleanupPlan =
            storageDuplicates.Count == 0 ? null : _duplicateCleanupService.BuildPlan(storageDuplicates);

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
