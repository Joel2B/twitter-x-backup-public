namespace Backup.Application.BackupRun;

public sealed class BackupRunStepExecutor : IBackupRunStepExecutor
{
    public async Task Run(
        IEnumerable<IBackupRunStep> steps,
        CancellationToken cancellationToken = default
    )
    {
        foreach (IBackupRunStep step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await step.Run(cancellationToken);
        }
    }
}
