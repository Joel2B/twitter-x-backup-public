using Backup.Application.BackupRun.Models;
using Backup.Domain.BackupRun;

namespace Backup.Application.BackupRun;

public interface IBackupRunPlanBuilder
{
    BackupRunPlan Build(BackupRunPlanInput input);
}
