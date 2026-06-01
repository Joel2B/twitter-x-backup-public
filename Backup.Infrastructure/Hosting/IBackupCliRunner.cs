namespace Backup.Infrastructure.Hosting;

public interface IBackupCliRunner
{
    Task RunBackup(CancellationToken cancellationToken = default);
}
