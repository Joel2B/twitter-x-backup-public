using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkFailureOrchestrationService
{
    IReadOnlyDictionary<string, MediaBackupChunkFailureState> BuildResetMapForApplyFailure(
        IEnumerable<MediaBackupChunkFailureState> items
    );
    IReadOnlyDictionary<string, MediaBackupChunkFailureState> BuildResetMapForCorruptChunk(
        IEnumerable<MediaBackupChunkFailureState> items
    );
}
