using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupProgressPolicyService
{
    MediaBackupProgressDecision Evaluate(int current, int total, int previousPercent);
}
