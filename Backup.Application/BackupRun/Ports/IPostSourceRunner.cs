using Backup.Application.BackupRun.Models;

namespace Backup.Application.BackupRun.Ports;

public interface IPostSourceRunner
{
    Task Run(BackupRunSourceExecution execution, CancellationToken cancellationToken = default);
}
