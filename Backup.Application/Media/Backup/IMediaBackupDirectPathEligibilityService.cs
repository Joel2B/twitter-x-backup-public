using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupDirectPathEligibilityService
{
    bool ShouldBackupDirect(MediaBackupDirectPathCandidate candidate, long maxPathSizeBytes);
}
