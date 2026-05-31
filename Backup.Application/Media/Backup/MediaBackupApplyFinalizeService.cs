using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupApplyFinalizeService(
    IMediaBackupStorageConsistencyDecisionService storageConsistencyDecisionService
) : IMediaBackupApplyFinalizeService
{
    private readonly IMediaBackupStorageConsistencyDecisionService _storageConsistencyDecisionService =
        storageConsistencyDecisionService;

    public MediaBackupApplyFinalizePlan Plan(
        IEnumerable<string> memoryPaths,
        IEnumerable<string> storagePaths,
        IEnumerable<int> existingChunkIds,
        int chunkId
    )
    {
        MediaBackupStorageConsistencyDecision decision =
            _storageConsistencyDecisionService.DecideForApply(memoryPaths, storagePaths);

        HashSet<int> ids = [.. existingChunkIds];
        ids.Add(chunkId);

        return new MediaBackupApplyFinalizePlan
        {
            ConsistencyDecision = decision,
            ChunkIds = ids.OrderBy(id => id).ToList(),
        };
    }
}
