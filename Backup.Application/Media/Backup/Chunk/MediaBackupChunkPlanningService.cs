using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkPlanningService : IMediaBackupChunkPlanningService
{
    public MediaBackupChunkPlanningResult Plan(
        int totalPathCount,
        int totalChunkCount,
        int backupIncrease,
        int configuredIncrease,
        IEnumerable<int> existingChunkIds
    )
    {
        int pathsPerChunk = totalChunkCount <= 0 ? 0 : totalPathCount / totalChunkCount;
        int increaseCount = Math.Max(backupIncrease, configuredIncrease);
        int capacityPerChunk = pathsPerChunk + increaseCount;

        HashSet<int> existing = [.. existingChunkIds];
        List<int> missingChunkIds = [];

        for (int i = 0; i < totalChunkCount; i++)
        {
            if (!existing.Contains(i))
                missingChunkIds.Add(i);
        }

        return new MediaBackupChunkPlanningResult
        {
            PathsPerChunk = pathsPerChunk,
            IncreaseCount = increaseCount,
            CapacityPerChunk = capacityPerChunk,
            MissingChunkIds = missingChunkIds,
            RequiresSeedChunk = existing.Count == 0,
        };
    }
}
