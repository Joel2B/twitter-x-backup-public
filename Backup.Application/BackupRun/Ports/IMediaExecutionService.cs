namespace Backup.Application.BackupRun.Ports;

public interface IMediaExecutionService
{
    Task Download(CancellationToken cancellationToken = default);
}
