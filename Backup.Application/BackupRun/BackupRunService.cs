using Backup.Application.BackupRun.Ports;
using Backup.Domain.BackupRun;

namespace Backup.Application.BackupRun;

public class BackupRunService(
    IBackupRunPlanProvider planProvider,
    IBackupRunExecutionMapper executionMapper,
    IPostSourceRunner postSourceRunner,
    IPostRecoveryRunner postRecoveryRunner,
    IBulkRunner bulkRunner,
    IMediaRunner mediaRunner,
    IPostStoreVerifier postStoreVerifier
) : IBackupRunService
{
    private readonly IBackupRunPlanProvider _planProvider = planProvider;
    private readonly IBackupRunExecutionMapper _executionMapper = executionMapper;
    private readonly IPostSourceRunner _postSourceRunner = postSourceRunner;
    private readonly IPostRecoveryRunner _postRecoveryRunner = postRecoveryRunner;
    private readonly IBulkRunner _bulkRunner = bulkRunner;
    private readonly IMediaRunner _mediaRunner = mediaRunner;
    private readonly IPostStoreVerifier _postStoreVerifier = postStoreVerifier;

    public async Task RunBackup()
    {
        BackupRunPlan plan = _planProvider.GetPlan();

        foreach (BackupRunUserPlan user in plan.Users)
        {
            foreach (BackupRunSourcePlan source in user.Sources)
                await _postSourceRunner.Run(_executionMapper.MapSource(user.UserId, source));
        }

        foreach (BackupRunUserPlan user in plan.Users.Where(user => user.RunRecovery))
            await _postRecoveryRunner.Run(_executionMapper.MapRecovery(user));

        if (plan.IsBulkEnabled)
        {
            foreach (BackupRunUserPlan user in plan.Users.Where(user => user.RunBulk))
                await _bulkRunner.Run(user.UserId);
        }

        if (plan.IsMediaEnabled)
            await _mediaRunner.Run();

        await _postStoreVerifier.Verify();
    }
}
