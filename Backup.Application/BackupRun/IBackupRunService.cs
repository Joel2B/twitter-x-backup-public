namespace Backup.Application.BackupRun;

public interface IBackupRunService
{
    Task RunBackup(CancellationToken cancellationToken = default);
}
