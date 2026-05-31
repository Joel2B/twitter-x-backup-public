using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupIntegrityPlanningService : IMediaBackupIntegrityPlanningService
{
    public bool HasChange(
        long? expectedFileSize,
        long? actualFileSize,
        long? expectedCrc32,
        long? actualCrc32
    ) => expectedFileSize != actualFileSize || expectedCrc32 != actualCrc32;

    public IReadOnlyList<MediaBackupIntegrityChunkGroup> GroupByChunk(
        IEnumerable<MediaBackupIntegrityPathChange> changes
    ) =>
        changes.GroupBy(change => change.ChunkId)
            .Select(group => new MediaBackupIntegrityChunkGroup
            {
                ChunkId = group.Key,
                Paths = group.Select(change => change.Path).ToList(),
            })
            .ToList();
}
