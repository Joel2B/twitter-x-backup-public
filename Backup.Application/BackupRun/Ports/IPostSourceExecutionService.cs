using Backup.Application.BackupRun.Models;

namespace Backup.Application.BackupRun.Ports;

public interface IPostSourceExecutionService
{
    Task Download(
        BackupRunSourceExecution execution,
        CancellationToken cancellationToken = default
    );
}
