using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkEntryStateMutationService
{
    IReadOnlyList<MediaBackupChunkEntryState> BuildStates(
        IEnumerable<MediaBackupChunkEntryMutationInput> items
    );

    IReadOnlyList<MediaBackupChunkEntryMutationInput> ApplyStates(
        IEnumerable<MediaBackupChunkEntryMutationInput> items,
        IEnumerable<MediaBackupChunkEntryState> states
    );
}
