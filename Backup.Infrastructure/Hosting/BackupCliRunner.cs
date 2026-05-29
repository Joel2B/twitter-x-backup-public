namespace Backup.Infrastructure.Hosting;

public sealed class BackupCliRunner(Backup.Application.BackupRun.IBackupRunService backupRunService)
    : IBackupCliRunner
{
    private readonly Backup.Application.BackupRun.IBackupRunService _backupRunService =
        backupRunService;

    public Task RunBackup() => _backupRunService.RunBackup();
}
