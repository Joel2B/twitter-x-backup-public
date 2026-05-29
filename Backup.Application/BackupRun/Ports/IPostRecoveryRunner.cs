using Backup.Domain.BackupRun;

namespace Backup.Application.BackupRun.Ports;

public interface IPostRecoveryRunner
{
    Task Run(BackupRunUserPlan user);
}
