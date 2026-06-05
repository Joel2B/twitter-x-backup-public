using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupApplyChunkPlanningService(
    IMediaBackupApplyEntrySelectionService mediaBackupApplyEntrySelectionService,
    IMediaBackupApplyFinalizeService mediaBackupApplyFinalizeService
) : IMediaBackupApplyChunkPlanningService
{
    private readonly IMediaBackupApplyEntrySelectionService _mediaBackupApplyEntrySelectionService =
        mediaBackupApplyEntrySelectionService;
    private readonly IMediaBackupApplyFinalizeService _mediaBackupApplyFinalizeService =
        mediaBackupApplyFinalizeService;

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

        IReadOnlyList<MediaBackupApplyEntryCandidate> toAdd =
            _mediaBackupApplyEntrySelectionService.SelectEntriesToAdd(candidates, storagePaths);
        HashSet<string> projectedStoragePaths = [.. storagePaths];

        foreach (MediaBackupApplyEntryCandidate item in toAdd)
            projectedStoragePaths.Add(item.ArchivePath);

        MediaBackupApplyFinalizePlan finalizePlan = _mediaBackupApplyFinalizeService.Plan(
            candidates.Select(item => item.ArchivePath),
            projectedStoragePaths,
            existingChunkIds,
            chunkId
        );

        return new MediaBackupApplyChunkPlan
        {
            ShouldProcessChunk = true,
            EntriesToAdd = toAdd,
            FinalizePlan = finalizePlan,
        };
    }
}
