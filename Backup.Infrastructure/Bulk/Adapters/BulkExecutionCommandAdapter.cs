using Backup.Application.Bulk;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Bulk.Adapters;

public sealed class BulkExecutionCommandAdapter(
    IReadOnlyDictionary<string, ApiConfig> api,
    IBulkImportRunner importRunner,
    IBulkVerifyRunner verifyRunner,
    IBulkPhase1Runner phase1Runner,
    IBulkPhase2Runner phase2Runner,
    IBulkPhase2ResetRunner phase2ResetRunner,
    IBulkData bulkData
) : IBulkExecutionCommand
{
    private readonly IReadOnlyDictionary<string, ApiConfig> _api = api;
    private readonly IBulkImportRunner _importRunner = importRunner;
    private readonly IBulkVerifyRunner _verifyRunner = verifyRunner;
    private readonly IBulkPhase1Runner _phase1Runner = phase1Runner;
    private readonly IBulkPhase2Runner _phase2Runner = phase2Runner;
    private readonly IBulkPhase2ResetRunner _phase2ResetRunner = phase2ResetRunner;
    private readonly IBulkData _bulkData = bulkData;

    public Task RunImport(CancellationToken cancellationToken) =>
        _importRunner.Run(_api, cancellationToken);

    public Task RunVerify(CancellationToken cancellationToken) =>
        _verifyRunner.Run(cancellationToken);

    public Task RunPhase1(CancellationToken cancellationToken) =>
        _phase1Runner.Run(_api, cancellationToken);

    public Task RunPhase2(CancellationToken cancellationToken) =>
        _phase2Runner.Run(_api, cancellationToken);

    public Task RunPhase2Reset(CancellationToken cancellationToken) =>
        _phase2ResetRunner.Run(cancellationToken);

    public Task Prune(CancellationToken cancellationToken) => _bulkData.Prune(cancellationToken);
}
