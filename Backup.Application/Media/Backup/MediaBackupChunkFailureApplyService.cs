using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkFailureApplyService(
    IMediaBackupChunkFailureOrchestrationService mediaBackupChunkFailureOrchestrationService,
    IMediaBackupChunkEntryStateService mediaBackupChunkEntryStateService
) : IMediaBackupChunkFailureApplyService
{
    private readonly IMediaBackupChunkFailureOrchestrationService _mediaBackupChunkFailureOrchestrationService =
        mediaBackupChunkFailureOrchestrationService;
    private readonly IMediaBackupChunkEntryStateService _mediaBackupChunkEntryStateService =
        mediaBackupChunkEntryStateService;

    public IReadOnlyList<MediaBackupChunkEntryState> ApplyForApplyFailure(
        IEnumerable<MediaBackupChunkEntryState> entries
    )
    {
        IReadOnlyList<MediaBackupChunkEntryState> entryList = entries.ToList();
        IReadOnlyDictionary<string, MediaBackupChunkFailureState> resetByPath =
            _mediaBackupChunkFailureOrchestrationService.BuildResetMapForApplyFailure(
                _mediaBackupChunkEntryStateService.BuildFailureStates(entryList)
            );

        return _mediaBackupChunkEntryStateService.ApplyFailureStates(entryList, resetByPath);
    }

    public IReadOnlyList<MediaBackupChunkEntryState> ApplyForCorruptChunk(
        IEnumerable<MediaBackupChunkEntryState> entries
    )
    {
        IReadOnlyList<MediaBackupChunkEntryState> entryList = entries.ToList();
        IReadOnlyDictionary<string, MediaBackupChunkFailureState> resetByPath =
            _mediaBackupChunkFailureOrchestrationService.BuildResetMapForCorruptChunk(
                _mediaBackupChunkEntryStateService.BuildFailureStates(entryList)
            );

        return _mediaBackupChunkEntryStateService.ApplyFailureStates(entryList, resetByPath);
    }
}
