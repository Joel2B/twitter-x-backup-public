using Backup.Application.BackupRun.Models;

namespace Backup.Application.BackupRun.Ports;

public interface IPostSourceRunner
{
    Task Run(string userId, BackupRunSourcePlan source);
}
