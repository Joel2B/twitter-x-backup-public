using Backup.Application.BackupRun.Models;

namespace Backup.Application.BackupRun.Ports;

public interface IPostRecoveryExecutionService
{
    Task Recover(BackupRunRecoveryExecution execution);
}
