using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupIntegrityPlanningService
{
    bool HasChange(
        long? expectedFileSize,
        long? actualFileSize,
        long? expectedCrc32,
        long? actualCrc32
    );

    IReadOnlyList<MediaBackupIntegrityChunkGroup> GroupByChunk(
        IEnumerable<MediaBackupIntegrityPathChange> changes
    );
}
