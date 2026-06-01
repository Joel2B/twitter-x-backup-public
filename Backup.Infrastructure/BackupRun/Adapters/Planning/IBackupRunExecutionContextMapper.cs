using Backup.Application.BackupRun.Models;
using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.BackupRun.Adapters;

public interface IBackupRunExecutionContextMapper
{
    ApiContext ToApiContext(BackupRunSourceExecution execution);
    UsersContext ToUsersContext(BackupRunRecoveryExecution execution);
}
