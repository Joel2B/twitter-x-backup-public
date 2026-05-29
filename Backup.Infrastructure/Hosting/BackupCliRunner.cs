namespace Backup.Infrastructure.Hosting;

public sealed class BackupCliRunner(BackupRuntime backupRuntime) : IBackupCliRunner
{
    private readonly BackupRuntime _backupRuntime = backupRuntime;

    public Task RunBackup() => _backupRuntime.RunBackup();
}
