namespace Backup.Infrastructure.Hosting;

public sealed class BackupCliRunner(global::Backup.App.App app) : IBackupCliRunner
{
    private readonly global::Backup.App.App _app = app;

    public Task RunBackup() => _app.Backup();
}
