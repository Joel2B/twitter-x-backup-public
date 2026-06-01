using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkAssignmentService
{
    MediaBackupChunkAssignmentResult Assign(
        IReadOnlyList<MediaBackupChunkState> chunks,
        IReadOnlyList<MediaBackupPathCandidate> candidates,
        int totalChunkCount,
        int pathsPerChunk,
        int increaseCount,
        long maxPathSizeBytes
    );
}
