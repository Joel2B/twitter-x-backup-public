using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDuplicateChunkExecutionService(
    IMediaBackupPathProjectionService mediaBackupPathProjectionService,
    IMediaBackupDuplicateCheckPlanningService mediaBackupDuplicateCheckPlanningService,
    IMediaBackupDuplicateChunkOrchestrationService mediaBackupDuplicateChunkOrchestrationService
) : IMediaBackupDuplicateChunkExecutionService
{
    private readonly IMediaBackupPathProjectionService _mediaBackupPathProjectionService =
        mediaBackupPathProjectionService;
    private readonly IMediaBackupDuplicateCheckPlanningService _mediaBackupDuplicateCheckPlanningService =
        mediaBackupDuplicateCheckPlanningService;
    private readonly IMediaBackupDuplicateChunkOrchestrationService _mediaBackupDuplicateChunkOrchestrationService =
        mediaBackupDuplicateChunkOrchestrationService;

    public MediaBackupDuplicateChunkExecutionResult Execute(
        IEnumerable<string> chunkPaths,
        IEnumerable<string> storageArchivePaths,
        int extraPreviewLimit
    )
    {
        IReadOnlyList<string> memory = _mediaBackupPathProjectionService.ToArchivePaths(chunkPaths);
        IReadOnlyList<string> storage = storageArchivePaths.ToList();

        MediaBackupDuplicateCheckPlan plan = _mediaBackupDuplicateCheckPlanningService.Plan(
            memory,
            storage
        );
        MediaBackupDuplicateChunkExecutionPlan executionPlan =
            _mediaBackupDuplicateChunkOrchestrationService.BuildExecutionPlan(
                plan,
                extraPreviewLimit
            );

        int removedExtrasCount = executionPlan.ShouldRemoveExtras
            ? executionPlan.ExtraPathsToRemove.Count
            : 0;

        return new MediaBackupDuplicateChunkExecutionResult
        {
            MemoryArchivePaths = memory,
            StorageArchivePaths = storage,
            ExecutionPlan = executionPlan,
            RemovedExtrasCount = removedExtrasCount,
        };
    }

    public int UpdateStorageCount(
        int currentStorageCount,
        MediaBackupDuplicateChunkExecutionResult executionResult
    ) =>
        _mediaBackupDuplicateChunkOrchestrationService.UpdateStorageCount(
            currentStorageCount,
            executionResult.StorageArchivePaths.Count,
            executionResult.RemovedExtrasCount
        );
}
