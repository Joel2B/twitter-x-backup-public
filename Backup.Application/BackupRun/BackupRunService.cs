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

    public async Task RunBackup(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BackupRunPlan plan = _planProvider.GetPlan();

        foreach (BackupRunUserPlan user in plan.Users)
        {
            foreach (BackupRunSourcePlan source in user.Sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _postSourceRunner.Run(
                    _executionMapper.MapSource(user.UserId, source),
                    cancellationToken
                );
            }
        }

        foreach (BackupRunUserPlan user in plan.Users.Where(user => user.RunRecovery))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _postRecoveryRunner.Run(_executionMapper.MapRecovery(user), cancellationToken);
        }

        if (plan.IsBulkEnabled)
        {
            foreach (BackupRunUserPlan user in plan.Users.Where(user => user.RunBulk))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _bulkRunner.Run(user.UserId, cancellationToken);
            }
        }

        if (plan.IsMediaEnabled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _mediaRunner.Run(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _postStoreVerifier.Verify(cancellationToken);
    }
}
