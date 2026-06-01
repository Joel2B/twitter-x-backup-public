namespace Backup.Application.BackupRun.Ports;

public interface IBulkRunner
{
    Task Run(string userId, CancellationToken cancellationToken = default);
}
