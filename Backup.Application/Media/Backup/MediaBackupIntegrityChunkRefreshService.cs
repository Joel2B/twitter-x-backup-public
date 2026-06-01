using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupIntegrityChunkRefreshService(
    IMediaBackupIntegrityChunkUpdateOrchestrationService mediaBackupIntegrityChunkUpdateOrchestrationService,
    IMediaBackupIntegrityChunkApplyService mediaBackupIntegrityChunkApplyService
) : IMediaBackupIntegrityChunkRefreshService
{
    private readonly IMediaBackupIntegrityChunkUpdateOrchestrationService _mediaBackupIntegrityChunkUpdateOrchestrationService =
        mediaBackupIntegrityChunkUpdateOrchestrationService;
    private readonly IMediaBackupIntegrityChunkApplyService _mediaBackupIntegrityChunkApplyService =
        mediaBackupIntegrityChunkApplyService;

    public MediaBackupIntegrityChunkApplyResult Refresh(
        IEnumerable<string> changedPaths,
        IEnumerable<string> chunkPaths,
        IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByPath,
        IEnumerable<MediaBackupChunkEntryState> entries
    )
    {
        MediaBackupIntegrityUpdateSelectionPlan selection =
            _mediaBackupIntegrityChunkUpdateOrchestrationService.SelectAndValidate(
                changedPaths,
                chunkPaths,
                metadataByPath
            );

        return _mediaBackupIntegrityChunkApplyService.Apply(entries, selection);
    }
}
