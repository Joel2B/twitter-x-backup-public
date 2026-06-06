using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupApplyChunkPlanningService(
    IMediaBackupStorageConsistencyDecisionService mediaBackupStorageConsistencyDecisionService
) : IMediaBackupApplyChunkPlanningService
{
    private readonly IMediaBackupStorageConsistencyDecisionService _mediaBackupStorageConsistencyDecisionService =
        mediaBackupStorageConsistencyDecisionService;

    public MediaBackupApplyChunkPlan Plan(
        IReadOnlyCollection<MediaBackupApplyChunkPathState> chunkPaths,
        ISet<string> storagePaths,
        IEnumerable<int> existingChunkIds,
        int chunkId
    )
    {
        List<MediaBackupApplyEntryCandidate> candidates = chunkPaths
            .Select(item => new MediaBackupApplyEntryCandidate
            {
                SourcePath = item.SourcePath,
                ArchivePath = MediaBackupPathProjection.ToArchivePath(item.SourcePath),
                HasHash = item.HasHash,
            })
            .ToList();

        if (!candidates.Any(item => item.HasHash))
        {
            return new MediaBackupApplyChunkPlan
            {
                ShouldProcessChunk = false,
                EntriesToAdd = [],
                FinalizePlan = null,
            };
        }

        IReadOnlyList<MediaBackupApplyEntryCandidate> toAdd = candidates
            .Where(item => item.HasHash && !storagePaths.Contains(item.ArchivePath))
            .ToList();

        HashSet<string> projectedStoragePaths = [.. storagePaths];

        foreach (MediaBackupApplyEntryCandidate item in toAdd)
            projectedStoragePaths.Add(item.ArchivePath);

        MediaBackupStorageConsistencyDecision decision =
            _mediaBackupStorageConsistencyDecisionService.DecideForApply(
                candidates.Select(item => item.ArchivePath),
                projectedStoragePaths
            );

        HashSet<int> ids = [.. existingChunkIds];
        ids.Add(chunkId);

        MediaBackupApplyFinalizePlan finalizePlan = new()
        {
            ConsistencyDecision = decision,
            ChunkIds = ids.OrderBy(id => id).ToList(),
        };

        return new MediaBackupApplyChunkPlan
        {
            ShouldProcessChunk = true,
            EntriesToAdd = toAdd,
            FinalizePlan = finalizePlan,
        };
    }
}
