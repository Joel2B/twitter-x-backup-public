using Backup.Application.BackupRun.Models;

namespace Backup.Application.BackupRun.Ports;

public interface IBackupRunPlanProvider
{
    BackupRunPlan GetPlan();
}
