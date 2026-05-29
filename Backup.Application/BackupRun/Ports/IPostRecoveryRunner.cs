using Backup.Application.BackupRun.Models;

namespace Backup.Application.BackupRun.Ports;

public interface IPostRecoveryRunner
{
    Task Run(BackupRunUserPlan user);
}
