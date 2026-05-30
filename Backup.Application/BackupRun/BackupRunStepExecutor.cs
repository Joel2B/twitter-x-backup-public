namespace Backup.Application.BackupRun;

public sealed class BackupRunStepExecutor : IBackupRunStepExecutor
{
    public async Task Run(IEnumerable<IBackupRunStep> steps)
    {
        foreach (IBackupRunStep step in steps)
            await step.Run();
    }
}
