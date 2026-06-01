using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkEntryStateOrchestrationService
{
    IReadOnlyList<MediaBackupChunkEntryState> BuildStates(
        IEnumerable<MediaBackupChunkEntryRecord> entries
    );

    IReadOnlyList<MediaBackupChunkEntryRecord> ApplyStates(
        IEnumerable<MediaBackupChunkEntryRecord> entries,
        IEnumerable<MediaBackupChunkEntryState> states
    );
}
