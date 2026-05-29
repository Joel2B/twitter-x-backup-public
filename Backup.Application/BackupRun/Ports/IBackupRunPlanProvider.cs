using Backup.Domain.BackupRun;

namespace Backup.Application.BackupRun.Ports;

public interface IBackupRunPlanProvider
{
    BackupRunPlan GetPlan();
}
