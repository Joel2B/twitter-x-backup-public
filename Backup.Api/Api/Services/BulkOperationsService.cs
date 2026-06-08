using Backup.Api.Models;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;

namespace Backup.Api.Services;

public sealed class BulkOperationsService(
    ConfigContextResolver contextResolver,
    Backup.Application.BackupRun.Ports.IBulkRunner bulkRunner,
    IBulkImportRunner importRunner,
    IBulkVerifyRunner verifyRunner,
    IBulkPhase1Runner phase1Runner,
    IBulkPhase2Runner phase2Runner,
    IBulkPhase2ResetRunner phase2ResetRunner,
    IBulkData bulkData,
    IEnumerable<IBulkDataStore> bulkDataStores,
    IEnumerable<IBulkSourceDataStore> bulkSourceDataStores
)
{
    private readonly ConfigContextResolver _contextResolver = contextResolver;
    private readonly Backup.Application.BackupRun.Ports.IBulkRunner _bulkRunner = bulkRunner;
    private readonly IBulkImportRunner _importRunner = importRunner;
    private readonly IBulkVerifyRunner _verifyRunner = verifyRunner;
    private readonly IBulkPhase1Runner _phase1Runner = phase1Runner;
    private readonly IBulkPhase2Runner _phase2Runner = phase2Runner;
    private readonly IBulkPhase2ResetRunner _phase2ResetRunner = phase2ResetRunner;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IReadOnlyList<IBulkDataStore> _bulkDataStores = [.. bulkDataStores];
    private readonly IReadOnlyList<IBulkSourceDataStore> _bulkSourceDataStores =
    [
        .. bulkSourceDataStores,
    ];

    public async Task<OperationResult> Run(
        BulkRunRequest request,
        CancellationToken cancellationToken
    )
    {
        OperationResult? skipped = ValidateStores("bulk-run", requireSourceStores: true);

        if (skipped is not null)
            return skipped;

        await _bulkRunner.Run(request.UserId, cancellationToken);
        return new OperationResult("bulk-run", "completed", $"user={request.UserId}");
    }

    public async Task<OperationResult> Import(
        BulkRunRequest request,
        CancellationToken cancellationToken
    )
    {
        OperationResult? skipped = ValidateStores("bulk-import", requireSourceStores: true);

        if (skipped is not null)
            return skipped;

        await _importRunner.Run(GetRequiredApi(request.UserId), cancellationToken);
        return new OperationResult("bulk-import", "completed", $"user={request.UserId}");
    }

    public async Task<OperationResult> Verify(CancellationToken cancellationToken)
    {
        OperationResult? skipped = ValidateStores("bulk-verify", requireSourceStores: false);

        if (skipped is not null)
            return skipped;

        await _verifyRunner.Run(cancellationToken);
        return new OperationResult("bulk-verify", "completed");
    }

    public async Task<OperationResult> Phase1(
        BulkRunRequest request,
        CancellationToken cancellationToken
    )
    {
        OperationResult? skipped = ValidateStores("bulk-phase1", requireSourceStores: true);

        if (skipped is not null)
            return skipped;

        await _phase1Runner.Run(GetRequiredApi(request.UserId), cancellationToken);
        return new OperationResult("bulk-phase1", "completed", $"user={request.UserId}");
    }

    public async Task<OperationResult> Phase2(
        BulkRunRequest request,
        CancellationToken cancellationToken
    )
    {
        OperationResult? skipped = ValidateStores("bulk-phase2", requireSourceStores: true);

        if (skipped is not null)
            return skipped;

        await _phase2Runner.Run(GetRequiredApi(request.UserId), cancellationToken);
        return new OperationResult("bulk-phase2", "completed", $"user={request.UserId}");
    }

    public async Task<OperationResult> ResetPhase2(CancellationToken cancellationToken)
    {
        OperationResult? skipped = ValidateStores("bulk-phase2-reset", requireSourceStores: false);

        if (skipped is not null)
            return skipped;

        await _phase2ResetRunner.Run(cancellationToken);
        return new OperationResult("bulk-phase2-reset", "completed");
    }

    public async Task<OperationResult> Prune(CancellationToken cancellationToken)
    {
        OperationResult? skipped = ValidateStores("bulk-prune", requireSourceStores: false);

        if (skipped is not null)
            return skipped;

        await _bulkData.Prune(cancellationToken);
        return new OperationResult("bulk-prune", "completed");
    }

    private IReadOnlyDictionary<
        string,
        Backup.Infrastructure.Models.Config.Api.ApiConfig
    > GetRequiredApi(string userId) => _contextResolver.GetRequiredUsersContext(userId).Api;

    private OperationResult? ValidateStores(string operation, bool requireSourceStores)
    {
        if (_bulkDataStores.Count == 0)
            return new OperationResult(operation, "skipped", "no bulk data stores configured");

        if (requireSourceStores && _bulkSourceDataStores.Count == 0)
        {
            return new OperationResult(
                operation,
                "skipped",
                "no bulk source data stores configured"
            );
        }

        return null;
    }
}
