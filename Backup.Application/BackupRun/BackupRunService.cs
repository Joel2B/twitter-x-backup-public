using Backup.Application.BackupRun.Ports;

namespace Backup.Application.BackupRun;

public class BackupRunService(
    IBackupRunPlanProvider planProvider,
    IPostSourceRunner postSourceRunner,
    IPostRecoveryRunner postRecoveryRunner,
    IBulkRunner bulkRunner,
    IMediaRunner mediaRunner,
    IPostStoreVerifier postStoreVerifier
) : IBackupRunService
{
    private readonly IBackupRunPlanProvider _planProvider = planProvider;
    private readonly IPostSourceRunner _postSourceRunner = postSourceRunner;
    private readonly IPostRecoveryRunner _postRecoveryRunner = postRecoveryRunner;
    private readonly IBulkRunner _bulkRunner = bulkRunner;
    private readonly IMediaRunner _mediaRunner = mediaRunner;
    private readonly IPostStoreVerifier _postStoreVerifier = postStoreVerifier;

    public async Task RunBackup()
    {
        Models.BackupRunPlan plan = _planProvider.GetPlan();

        foreach (Models.BackupRunUserPlan user in plan.Users)
        {
            foreach (Models.BackupRunSourcePlan source in user.Sources)
                await _postSourceRunner.Run(user.UserId, source);
        }

        foreach (Models.BackupRunUserPlan user in plan.Users.Where(user => user.RunRecovery))
            await _postRecoveryRunner.Run(user.UserId);

        if (plan.IsBulkEnabled)
        {
            foreach (Models.BackupRunUserPlan user in plan.Users.Where(user => user.RunBulk))
                await _bulkRunner.Run(user.UserId);
        }

        if (plan.IsMediaEnabled)
            await _mediaRunner.Run();

        await _postStoreVerifier.Verify();
    }
}
