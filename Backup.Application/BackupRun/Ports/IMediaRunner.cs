namespace Backup.Application.BackupRun.Ports;

public interface IMediaRunner
{
    Task Run(CancellationToken cancellationToken = default);
}
