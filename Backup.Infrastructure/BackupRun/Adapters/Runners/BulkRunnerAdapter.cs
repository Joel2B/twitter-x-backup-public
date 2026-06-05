using Backup.Application.BackupRun.Ports;
using Backup.Application.Bulk;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Bulk.Adapters;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class BulkRunnerAdapter(
    AppConfig config,
    IBulkExecutionService bulkExecutionService,
    IBulkImportRunner importRunner,
    IBulkVerifyRunner verifyRunner,
    IBulkPhase1Runner phase1Runner,
    IBulkPhase2Runner phase2Runner,
    IBulkPhase2ResetRunner phase2ResetRunner,
    IEnumerable<IBulkDataStore> bulkDataStores,
    IEnumerable<IBulkSourceDataStore> bulkSourceDataStores,
    IBulkData bulkData,
    ILogger<BulkRunnerAdapter> logger
) : IBulkRunner
{
    private readonly AppConfig _config = config;
    private readonly IBulkExecutionService _bulkExecutionService = bulkExecutionService;
    private readonly IBulkImportRunner _importRunner = importRunner;
    private readonly IBulkVerifyRunner _verifyRunner = verifyRunner;
    private readonly IBulkPhase1Runner _phase1Runner = phase1Runner;
    private readonly IBulkPhase2Runner _phase2Runner = phase2Runner;
    private readonly IBulkPhase2ResetRunner _phase2ResetRunner = phase2ResetRunner;
    private readonly IReadOnlyList<IBulkDataStore> _bulkDataStores = [.. bulkDataStores];
    private readonly IReadOnlyList<IBulkSourceDataStore> _bulkSourceDataStores =
    [
        .. bulkSourceDataStores,
    ];
    private readonly IBulkData _bulkData = bulkData;
    private readonly ILogger<BulkRunnerAdapter> _logger = logger;

    public async Task Run(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UsersContext? userContext = _config.UsersContext.FirstOrDefault(context =>
            context.UserId == userId
        );

        if (userContext is null)
            return;

        if (_bulkDataStores.Count == 0)
        {
            _logger.LogInformation(
                "bulk skipped for {UserId}: no bulk data stores configured",
                userId
            );
            return;
        }

        if (_bulkSourceDataStores.Count == 0)
        {
            _logger.LogInformation(
                "bulk skipped for {UserId}: no bulk source data stores configured",
                userId
            );
            return;
        }

        using (_logger.LogTimer("bulk execution service"))
            await _bulkExecutionService.Run(
                new BulkExecutionCommandAdapter(
                    userContext.Api,
                    _importRunner,
                    _verifyRunner,
                    _phase1Runner,
                    _phase2Runner,
                    _phase2ResetRunner,
                    _bulkData
                ),
                cancellationToken
            );
    }
}
