using Backup.Application.BackupRun.Models;
using Backup.Domain.BackupRun;

namespace Backup.Application.BackupRun;

public interface IBackupRunExecutionMapper
{
    BackupRunSourceExecution MapSource(string userId, BackupRunSourcePlan source);
    BackupRunRecoveryExecution MapRecovery(BackupRunUserPlan user);
}
