using Backup.Api.Models;
using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Ports;
using Backup.Domain.BackupRun;
using Backup.Infrastructure.Logging;

namespace Backup.Api.Services;

public sealed class BackupOperationsService(
    IBackupRunService backupRunService,
    IBackupRunPlanProvider planProvider,
    IBackupRunExecutionMapper executionMapper,
    IPostSourceRunner postSourceRunner,
    IPostRecoveryRunner postRecoveryRunner,
    IBulkRunner bulkRunner,
    IMediaRunner mediaRunner,
    IPostStoreVerifier postStoreVerifier
)
{
    private readonly IBackupRunService _backupRunService = backupRunService;
    private readonly IBackupRunPlanProvider _planProvider = planProvider;
    private readonly IBackupRunExecutionMapper _executionMapper = executionMapper;
    private readonly IPostSourceRunner _postSourceRunner = postSourceRunner;
    private readonly IPostRecoveryRunner _postRecoveryRunner = postRecoveryRunner;
    private readonly IBulkRunner _bulkRunner = bulkRunner;
    private readonly IMediaRunner _mediaRunner = mediaRunner;
    private readonly IPostStoreVerifier _postStoreVerifier = postStoreVerifier;

    public BackupPlanResponse GetPlan()
    {
        BackupRunPlan plan = _planProvider.GetPlan();

        return new BackupPlanResponse
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            IsBulkEnabled = plan.IsBulkEnabled,
            IsMediaEnabled = plan.IsMediaEnabled,
            Users = plan.Users.Select(MapUser).ToList(),
        };
    }

    public async Task<OperationResult> Run(CancellationToken cancellationToken)
    {
        await _backupRunService.RunBackup(cancellationToken);
        return new OperationResult("backup-run", "completed");
    }

    public async Task<OperationResult> RunPosts(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BackupRunPlan plan = _planProvider.GetPlan();
        int sourceCount = 0;

        foreach (BackupRunUserPlan user in plan.Users)
        {
            foreach (BackupRunSourcePlan source in user.Sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _postSourceRunner.Run(
                    _executionMapper.MapSource(user.UserId, source),
                    cancellationToken
                );
                sourceCount++;
            }
        }

        return new OperationResult(
            "backup-posts",
            "completed",
            $"users={plan.Users.Count}, sources={sourceCount}"
        );
    }

    public async Task<OperationResult> RunRecovery(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BackupRunPlan plan = _planProvider.GetPlan();
        List<BackupRunUserPlan> users = plan.Users.Where(user => user.RunRecovery).ToList();

        foreach (BackupRunUserPlan user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _postRecoveryRunner.Run(_executionMapper.MapRecovery(user), cancellationToken);
        }

        return new OperationResult("backup-recovery", "completed", $"users={users.Count}");
    }

    public async Task<OperationResult> RunBulk(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BackupRunPlan plan = _planProvider.GetPlan();

        if (!plan.IsBulkEnabled)
            return new OperationResult("backup-bulk", "skipped", "bulk is disabled");

        List<BackupRunUserPlan> users = plan.Users.Where(user => user.RunBulk).ToList();

        foreach (BackupRunUserPlan user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _bulkRunner.Run(user.UserId, cancellationToken);
        }

        return new OperationResult("backup-bulk", "completed", $"users={users.Count}");
    }

    public async Task<OperationResult> RunMedia(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BackupRunPlan plan = _planProvider.GetPlan();

        if (!plan.IsMediaEnabled)
            return new OperationResult("backup-media", "skipped", "media is disabled");

        await _mediaRunner.Run(cancellationToken);
        return new OperationResult("backup-media", "completed");
    }

    public async Task<OperationResult> VerifyPostStores(CancellationToken cancellationToken)
    {
        await _postStoreVerifier.Verify(cancellationToken);
        return new OperationResult("backup-verify-post-stores", "completed");
    }

    private static BackupPlanUserSummary MapUser(BackupRunUserPlan user) =>
        new()
        {
            UserId = user.UserId,
            RunRecovery = user.RunRecovery,
            RunBulk = user.RunBulk,
            Sources = user.Sources.Select(MapSource).ToList(),
            Api = user
                .Api.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key, entry => MapApi(entry.Value)),
        };

    private static BackupPlanSourceSummary MapSource(BackupRunSourcePlan source) =>
        new()
        {
            SourceId = source.SourceId,
            ApiId = source.ApiId,
            Count = source.Count,
            Request = MapRequest(source.Request),
        };

    private static BackupPlanApiSummary MapApi(BackupRunApiPlan api) =>
        new()
        {
            Id = api.Id,
            Enabled = api.Enabled,
            Request = MapRequest(api.Request),
        };

    private static BackupRequestSummary MapRequest(BackupRunRequestPlan request) =>
        new()
        {
            Url = request.Url,
            Variables = new Dictionary<string, object?>(request.Variables, StringComparer.Ordinal),
            Features = new Dictionary<string, bool>(request.Features, StringComparer.Ordinal),
            FieldToggles = new Dictionary<string, bool>(
                request.FieldToggles,
                StringComparer.Ordinal
            ),
            Headers = new Dictionary<string, string>(
                HttpHeaderSanitizer.Sanitize(request.Headers),
                StringComparer.OrdinalIgnoreCase
            ),
        };
}
