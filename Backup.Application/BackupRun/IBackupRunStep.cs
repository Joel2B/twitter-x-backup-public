namespace Backup.Application.BackupRun;

public interface IBackupRunStep
{
    Task Run(CancellationToken cancellationToken = default);
}
