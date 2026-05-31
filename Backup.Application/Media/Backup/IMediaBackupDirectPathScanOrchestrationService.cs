using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupDirectPathScanOrchestrationService
{
    MediaBackupDirectPathScanResult Evaluate(MediaBackupDirectPathCandidateObservation observation);
}
