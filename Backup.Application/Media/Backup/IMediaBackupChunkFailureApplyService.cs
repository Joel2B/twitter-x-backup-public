using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkFailureApplyService
{
    IReadOnlyList<MediaBackupChunkEntryState> ApplyForApplyFailure(
        IEnumerable<MediaBackupChunkEntryState> entries
    );

    IReadOnlyList<MediaBackupChunkEntryState> ApplyForCorruptChunk(
        IEnumerable<MediaBackupChunkEntryState> entries
    );
}
