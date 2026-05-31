using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDirectPathEligibilityService
    : IMediaBackupDirectPathEligibilityService
{
    public bool ShouldBackupDirect(MediaBackupDirectPathCandidate candidate, long maxPathSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(candidate.CachePath))
            return false;

        if (candidate.TargetExists)
            return false;

        return candidate.FileSizeBytes is long fileSize && fileSize > maxPathSizeBytes;
    }
}
