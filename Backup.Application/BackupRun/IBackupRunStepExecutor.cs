namespace Backup.Application.BackupRun;

public interface IBackupRunStepExecutor
{
    Task Run(IEnumerable<IBackupRunStep> steps);
}
