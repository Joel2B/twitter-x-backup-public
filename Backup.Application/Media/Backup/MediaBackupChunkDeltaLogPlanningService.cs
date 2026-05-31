using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkDeltaLogPlanningService
    : IMediaBackupChunkDeltaLogPlanningService
{
    public MediaBackupChunkDeltaLogPlan Plan(
        IEnumerable<MediaBackupChunkDeltaLogInput> inputs,
        int totalAddedPaths,
        int addedPathCount
    )
    {
        List<MediaBackupChunkDeltaLogRow> rows = inputs
            .Where(item => item.BeforeCount != item.AfterCount)
            .Select(item => new MediaBackupChunkDeltaLogRow
            {
                ChunkId = item.ChunkId,
                BeforeCount = item.BeforeCount,
                AfterCount = item.AfterCount,
                Difference = item.Difference,
                SizeBeforeGiB = ToGiB(item.SizeBeforeBytes),
                SizeAfterGiB = ToGiB(item.SizeAfterBytes),
            })
            .ToList();

        return new MediaBackupChunkDeltaLogPlan
        {
            TotalAddedPaths = totalAddedPaths,
            AddedPathCount = addedPathCount,
            Rows = rows,
        };
    }

    private static decimal ToGiB(long bytes) =>
        Math.Round(bytes / 1024m / 1024m / 1024m, 2, MidpointRounding.ToZero);
}
