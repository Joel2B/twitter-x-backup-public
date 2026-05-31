using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupProgressPolicyService : IMediaBackupProgressPolicyService
{
    public MediaBackupProgressDecision Evaluate(int current, int total, int previousPercent)
    {
        int safeTotal = Math.Max(total, 1);
        int percent = (int)((long)current * 100 / safeTotal);

        return new MediaBackupProgressDecision
        {
            Percent = percent,
            ShouldLog = percent != previousPercent,
        };
    }
}
