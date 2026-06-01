using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkFailureOrchestrationService(
    IMediaBackupChunkFailurePolicyService mediaBackupChunkFailurePolicyService
) : IMediaBackupChunkFailureOrchestrationService
{
    private readonly IMediaBackupChunkFailurePolicyService _mediaBackupChunkFailurePolicyService =
        mediaBackupChunkFailurePolicyService;

    public IReadOnlyDictionary<string, MediaBackupChunkFailureState> BuildResetMapForApplyFailure(
        IEnumerable<MediaBackupChunkFailureState> items
    ) =>
        _mediaBackupChunkFailurePolicyService
            .ResetForApplyFailure(items)
            .ToDictionary(item => item.Path, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, MediaBackupChunkFailureState> BuildResetMapForCorruptChunk(
        IEnumerable<MediaBackupChunkFailureState> items
    ) =>
        _mediaBackupChunkFailurePolicyService
            .ResetForCorruptChunk(items)
            .ToDictionary(item => item.Path, StringComparer.Ordinal);
}
