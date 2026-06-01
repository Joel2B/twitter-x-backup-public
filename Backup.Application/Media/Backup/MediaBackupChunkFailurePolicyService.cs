using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkFailurePolicyService : IMediaBackupChunkFailurePolicyService
{
    public IReadOnlyList<MediaBackupChunkFailureState> ResetForCorruptChunk(
        IEnumerable<MediaBackupChunkFailureState> items
    ) =>
        items
            .Select(item => new MediaBackupChunkFailureState
            {
                Path = item.Path,
                Hash = null,
                FileSize = null,
                Crc32 = null,
            })
            .ToList();

    public IReadOnlyList<MediaBackupChunkFailureState> ResetForApplyFailure(
        IEnumerable<MediaBackupChunkFailureState> items
    ) =>
        items
            .Select(item => new MediaBackupChunkFailureState
            {
                Path = item.Path,
                Hash = null,
                FileSize = item.FileSize,
                Crc32 = item.Crc32,
            })
            .ToList();
}
