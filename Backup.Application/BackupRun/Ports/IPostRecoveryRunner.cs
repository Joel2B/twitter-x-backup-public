namespace Backup.Application.BackupRun.Ports;

public interface IPostRecoveryRunner
{
    Task Run(string userId);
}
