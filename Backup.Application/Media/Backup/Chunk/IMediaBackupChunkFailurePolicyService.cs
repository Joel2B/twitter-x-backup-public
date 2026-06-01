using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkFailurePolicyService
{
    IReadOnlyList<MediaBackupChunkFailureState> ResetForCorruptChunk(
        IEnumerable<MediaBackupChunkFailureState> items
    );

    IReadOnlyList<MediaBackupChunkFailureState> ResetForApplyFailure(
        IEnumerable<MediaBackupChunkFailureState> items
    );
}
